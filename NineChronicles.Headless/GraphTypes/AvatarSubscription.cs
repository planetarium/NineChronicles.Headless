using Bencodex.Types;
using GraphQL.Types;
using Lib9c.Renderer;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using GraphQL.Subscription;
using GraphQL;
using GraphQL.Resolvers;
using Org.BouncyCastle.Utilities;

namespace NineChronicles.Headless.GraphTypes
{
    internal class AvatarSubscription : ObjectGraphType
    {
        private const int _blockRenderDegreeOfParallelism = 8;

        private BlockHeader? _tipHeader;
        private ISubject<TipChanged> _subject = new ReplaySubject<TipChanged>();
        public ConcurrentDictionary<Address,
                (ReplaySubject<AvatarState> avatarStateSubject, ReplaySubject<DailyRewardStatus> dailyRewardSubject)>
            agentAddresses
        { get; } = new ConcurrentDictionary<Address,
                (ReplaySubject<AvatarState>, ReplaySubject<DailyRewardStatus>)>();
        class TipChanged : ObjectGraphType<TipChanged>
        {
            public long Index { get; set; }

            public BlockHash Hash { get; set; }

            public TipChanged()
            {
                Field<NonNullGraphType<LongGraphType>>(nameof(Index));
                Field<ByteStringType>("hash", resolve: context => context.Source.Hash.ToByteArray());
            }
        }

        private NineChroniclesNodeService nineChroniclesNodeService { get; set; }

        public AvatarSubscription(NineChroniclesNodeService service)
        {
            AddField(new EventStreamFieldType
            {
                Name = $"{nameof(DailyRewardStatus)}ByAgent",
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "A hex-encoded address of an agent.",
                        Name = "agentAddress",
                    }
                ),
                Type = typeof(NonNullGraphType<DailyRewardStatusType>),
                Resolver = new FuncFieldResolver<DailyRewardStatus>(context => (context.Source as DailyRewardStatus)!),
                Subscriber = new EventStreamResolver<DailyRewardStatus>(SubscribeDailyReward),
            });

            nineChroniclesNodeService = service;
            BlockRenderer blockRenderer = service.BlockRenderer;
            blockRenderer.BlockSubject
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderBlock);

            ActionRenderer actionRenderer = service.ActionRenderer;
            actionRenderer.EveryRender<DailyReward>()
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderDailyRewardStateSubject);
        }

        private void RenderDailyRewardStateSubject<T>(ActionBase.ActionEvaluation<T> eval)
            where T : ActionBase
        {
            if (!(nineChroniclesNodeService is { } service))
            {
                throw new InvalidOperationException(
                    $"{nameof(NineChroniclesNodeService)} is null.");
            }

            // Skip when error.
            if (eval.Exception is { })
            {
                return;
            }

            foreach (var (address, subjects) in agentAddresses)
            {
                if (eval.Signer.Equals(address) &&
                    service.BlockChain.GetState(address, _tipHeader?.Hash) is Dictionary agentDict)
                {
                    var agentState = new AgentState(agentDict);


                    var subject = subjects.avatarStateSubject;
                    var rewardSubect = subjects.dailyRewardSubject;
                    var lastRewardIndexes = new List<long>();
                    var actionPoints = new List<int>();
                    foreach (var avatarAddress in agentState.avatarAddresses)
                    {
                        if (service.BlockChain.GetState(avatarAddress.Value, _tipHeader?.Hash) is Dictionary avatarDict)
                        {
                            var avatarState = new AvatarState(avatarDict);
                            lastRewardIndexes.Add(avatarState.dailyRewardReceivedIndex);
                            actionPoints.Add(avatarState.actionPoint);
                        }
                    }
                    var dailyReward = new DailyRewardStatus(lastRewardIndexes, actionPoints);
                    rewardSubect.OnNext(dailyReward);

                }
            }
        }

        private void RenderBlock((Block<PolymorphicAction<ActionBase>> OldTip, Block<PolymorphicAction<ActionBase>> NewTip) pair)
        {
            _tipHeader = pair.NewTip.Header;
            _subject.OnNext(
                new TipChanged
                {
                    Index = pair.NewTip.Index,
                    Hash = pair.NewTip.Hash,
                }
            );

            if (nineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            Log.Debug("AvatarSubscription.RenderBlock started");

            BlockChain<PolymorphicAction<ActionBase>> blockChain = nineChroniclesNodeService.BlockChain;
            Currency currency =
                new GoldCurrencyState(
                    (Dictionary)blockChain.GetState(Addresses.GoldCurrency, _tipHeader.Hash)
                ).Currency;
            var rewardSheet = new MonsterCollectionRewardSheet();
            var csv = blockChain.GetState(
                Addresses.GetSheetAddress<MonsterCollectionRewardSheet>(),
                _tipHeader.Hash
            ).ToDotnetString();
            rewardSheet.Set(csv);
            Log.Debug($"AvatarSubscription.RenderBlock target addresses. (count: {agentAddresses.Count})");
            agentAddresses
                .AsParallel()
                .WithDegreeOfParallelism(_blockRenderDegreeOfParallelism)
                .ForAll(kv =>
                {
                    Address address = kv.Key;
                    (ReplaySubject<AvatarState> avatarStateSubject, ReplaySubject<DailyRewardStatus> dailyRewardSubject) =
                        kv.Value;
                    RenderForAgent(
                        blockChain,
                        _tipHeader,
                        address,
                       // avatarStateSubject,
                        dailyRewardSubject
                    );
                });

            sw.Stop();
            Log.Debug($"StandaloneSubscription.RenderBlock ended. elapsed: {sw.Elapsed}");
        }

        private void RenderForAgent(
            BlockChain<PolymorphicAction<ActionBase>> blockChain,
            BlockHeader tipHeader,
            Address address,
            //ReplaySubject<AvatarState> avatarStateSubject,
            ReplaySubject<DailyRewardStatus> dailyRewardSubject)
        {

            if (blockChain.GetState(address, tipHeader.Hash) is Dictionary rawAgent)
            {
                var agentState = new AgentState(rawAgent);
                var lastRewardIndexes = new List<long>();
                var actionPoints = new List<int>();
                foreach (var avatarAddress in agentState.avatarAddresses)
                {
                    if (blockChain.GetState(avatarAddress.Value, _tipHeader?.Hash) is Dictionary avatarDict)
                    {
                        var avatarState = new AvatarState(avatarDict);
                        lastRewardIndexes.Add(avatarState.dailyRewardReceivedIndex);
                        actionPoints.Add(avatarState.actionPoint);
                    }
                }
                var dailyReward = new DailyRewardStatus(lastRewardIndexes, actionPoints);
                dailyRewardSubject.OnNext(dailyReward);

            }
        }

        private IObservable<DailyRewardStatus> SubscribeDailyReward(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("address");

            agentAddresses.TryAdd(address,
                (new ReplaySubject<AvatarState>(), new ReplaySubject<DailyRewardStatus>()));
            agentAddresses.TryGetValue(address, out var subjects);
            return subjects.dailyRewardSubject.AsObservable();
        }
    }
}
