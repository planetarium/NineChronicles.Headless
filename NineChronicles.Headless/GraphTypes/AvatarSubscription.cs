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
    public partial class StandaloneSubscription : ObjectGraphType
    {
        //private const int _blockRenderDegreeOfParallelism = 8;

        //private BlockHeader? _tipHeader;
        //private ISubject<TipChanged> _subject = new ReplaySubject<TipChanged>();
        private ConcurrentDictionary<Address,
                (ReplaySubject<AvatarActionPointStatus> hackAndSlashSubject, ReplaySubject<DailyRewardStatus> dailyRewardSubject)>
            agentAvatarAddresses
        { get; } = new ConcurrentDictionary<Address,
                (ReplaySubject<AvatarActionPointStatus>, ReplaySubject<DailyRewardStatus>)>();
        //class TipChanged : ObjectGraphType<TipChanged>
        //{
        //    public long Index { get; set; }

        //    public BlockHash Hash { get; set; }

        //    public TipChanged()
        //    {
        //        Field<NonNullGraphType<LongGraphType>>(nameof(Index));
        //        Field<ByteStringType>("hash", resolve: context => context.Source.Hash.ToByteArray());
        //    }
        //}
        //private NineChroniclesNodeService nineChroniclesNodeService { get; set; }
        public void AvatarSubscription(NineChroniclesNodeService service)
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
            AddField(new EventStreamFieldType
            {
                Name = $"{nameof(AvatarActionPointStatus)}ByAgent",
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "A hex-encoded address of an agent.",
                        Name = "agentAddress",
                    }
                    ),
                Type = typeof(NonNullGraphType<AvatarActionPointStatusType>),
                Resolver = new FuncFieldResolver<AvatarActionPointStatus>(context => (context.Source as AvatarActionPointStatus)!),
                Subscriber = new EventStreamResolver<AvatarActionPointStatus>(SubscribeHackAndSlash)
            });

            //nineChroniclesNodeService = service;
            //BlockRenderer blockRenderer = service.BlockRenderer;
            //blockRenderer.BlockSubject
            //    .ObserveOn(NewThreadScheduler.Default)
            //    .Subscribe(RenderAvatarBlock);
            ActionRenderer actionRenderer = service.ActionRenderer;
            actionRenderer.EveryRender<DailyReward>()
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderDailyRewardStateSubject);
        }

        

        private void RenderDailyRewardStateSubject<T>(ActionBase.ActionEvaluation<T> eval)
            where T : ActionBase
        {
            if (!(StandaloneContext.NineChroniclesNodeService is { } service))
            {
                throw new InvalidOperationException(
                    $"{nameof(NineChroniclesNodeService)} is null.");
            }

            // Skip when error.
            if (eval.Exception is { })
            {
                return;
            }

            foreach (var (address, subjects) in agentAvatarAddresses)
            {
                if (eval.Signer.Equals(address) &&
                    service.BlockChain.GetState(address, _tipHeader?.Hash) is Dictionary agentDict)
                {
                    var agentState = new AgentState(agentDict);
                    var subject = subjects.hackAndSlashSubject;
                    var rewardSubect = subjects.dailyRewardSubject;
                    foreach (var avatarAddress in agentState.avatarAddresses)
                    {
                        if(((eval.Action is HackAndSlash hackAndSlashAction && hackAndSlashAction?.AvatarAddress.Equals(avatarAddress) == true) 
                            || (eval.Action is HackAndSlashSweep hackAndSlashSweepAction && hackAndSlashSweepAction?.avatarAddress.Equals(avatarAddress) == true)
                            || (eval.Action is Grinding grindingAction && grindingAction?.AvatarAddress.Equals(avatarAddress) == true)
                            || (eval.Action is ChargeActionPoint chargeAction && chargeAction?.avatarAddress.Equals(avatarAddress) == true)
                            || (eval.Action is DailyReward rewardAction && rewardAction?.avatarAddress.Equals(avatarAddress) == true))
                            && service.BlockChain.GetState(avatarAddress.Value, _tipHeader?.Hash) is Dictionary avatarHnS)
                        {
                            var avatarState = new AvatarState(avatarHnS);
                            var hns = new AvatarActionPointStatus(_tipHeader!.Index, avatarState.actionPoint, avatarAddress.Value, avatarState.exp, avatarState.level, avatarState.inventory);
                            subject.OnNext(hns);
                            if (eval.Action is DailyReward)
                            {
                                var avatarReward = new DailyRewardStatus(avatarState.dailyRewardReceivedIndex, avatarState.actionPoint, avatarAddress.Value);
                                rewardSubect.OnNext(avatarReward);
                            }
                        }
                    }
                }
            }
        }

        private void RenderAvatarBlock((Block<PolymorphicAction<ActionBase>> OldTip, Block<PolymorphicAction<ActionBase>> NewTip) pair)
        {
            _tipHeader = pair.NewTip.Header;
            _subject.OnNext(
                new TipChanged
                {
                    Index = pair.NewTip.Index,
                    Hash = pair.NewTip.Hash,
                }
            );

            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            Log.Debug("AvatarSubscription.RenderBlock started");

            BlockChain<PolymorphicAction<ActionBase>> blockChain = StandaloneContext.NineChroniclesNodeService.BlockChain;
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
            Log.Debug($"AvatarSubscription.RenderBlock target addresses. (count: {agentAvatarAddresses.Count})");
            agentAvatarAddresses
                .AsParallel()
                .WithDegreeOfParallelism(_blockRenderDegreeOfParallelism)
                .ForAll(kv =>
                {
                    Address address = kv.Key;
                    (ReplaySubject<AvatarActionPointStatus> hackAndSlashSubject, ReplaySubject<DailyRewardStatus> dailyRewardSubject) =
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
                //var lastRewardIndexes = new List<long>();
                //var actionPoints = new List<int>();
                //foreach (var avatarAddress in agentState.avatarAddresses)
                //{
                //    if (blockChain.GetState(avatarAddress.Value, _tipHeader?.Hash) is Dictionary avatarDict)
                //    {
                //        var avatarState = new AvatarState(avatarDict);
                //        lastRewardIndexes.Add(avatarState.dailyRewardReceivedIndex);
                //        actionPoints.Add(avatarState.actionPoint);
                //    }
                //}
                var dailyReward = new DailyRewardStatus(0, 0, new Address());
                dailyRewardSubject.OnNext(dailyReward);

            }
        }

        private IObservable<DailyRewardStatus> SubscribeDailyReward(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("agentAddress");

            agentAvatarAddresses.TryAdd(address,
                (new ReplaySubject<AvatarActionPointStatus>(), new ReplaySubject<DailyRewardStatus>()));
            agentAvatarAddresses.TryGetValue(address, out var subjects);
            return subjects.dailyRewardSubject.AsObservable();
        }
        private IObservable<AvatarActionPointStatus?> SubscribeHackAndSlash(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("agentAddress");

            agentAvatarAddresses.TryAdd(address,
                (new ReplaySubject<AvatarActionPointStatus>(), new ReplaySubject<DailyRewardStatus>()));
            agentAvatarAddresses.TryGetValue(address, out var subjects);
            return subjects.hackAndSlashSubject.AsObservable();
        }
    }
}
