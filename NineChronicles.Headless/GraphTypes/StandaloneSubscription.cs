using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Subscription;
using GraphQL.Types;
using Lib9c.Renderer;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Headless;
using Libplanet.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using Libplanet.Blockchain;
using Serilog;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class StandaloneSubscription : ObjectGraphType
    {
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

        class PreloadStateType : ObjectGraphType<PreloadState>
        {
            private sealed class PreloadStateExtra
            {
                public string Type { get; }
                public long CurrentCount { get; }
                public long TotalCount { get; }

                public PreloadStateExtra(string type, long currentCount, long totalCount)
                {
                    Type = type;
                    CurrentCount = currentCount;
                    TotalCount = totalCount;
                }
            }

            private sealed class PreloadStateExtraType : ObjectGraphType<PreloadStateExtra>
            {
                public PreloadStateExtraType()
                {
                    Field<NonNullGraphType<StringGraphType>>(nameof(PreloadStateExtra.Type));
                    Field<NonNullGraphType<LongGraphType>>(nameof(PreloadStateExtra.CurrentCount));
                    Field<NonNullGraphType<LongGraphType>>(nameof(PreloadStateExtra.TotalCount));
                }
            }

            public PreloadStateType()
            {
                Field<NonNullGraphType<LongGraphType>>(name: "currentPhase", resolve: context => context.Source.CurrentPhase);
                Field<NonNullGraphType<LongGraphType>>(name: "totalPhase", resolve: context => PreloadState.TotalPhase);
                Field<NonNullGraphType<PreloadStateExtraType>>(name: "extra", resolve: context =>
                {
                    var preloadState = context.Source;
                    return preloadState switch
                    {
                        ActionExecutionState actionExecutionState => new PreloadStateExtra(nameof(ActionExecutionState),
                            actionExecutionState.ExecutedBlockCount,
                            actionExecutionState.TotalBlockCount),
                        BlockDownloadState blockDownloadState => new PreloadStateExtra(nameof(BlockDownloadState),
                            blockDownloadState.ReceivedBlockCount,
                            blockDownloadState.TotalBlockCount),
                        BlockHashDownloadState blockHashDownloadState => new PreloadStateExtra(
                            nameof(BlockHashDownloadState),
                            blockHashDownloadState.ReceivedBlockHashCount,
                            blockHashDownloadState.EstimatedTotalBlockHashCount),
                        BlockVerificationState blockVerificationState => new PreloadStateExtra(
                            nameof(BlockVerificationState),
                            blockVerificationState.VerifiedBlockCount,
                            blockVerificationState.TotalBlockCount),
                        StateDownloadState stateDownloadState => new PreloadStateExtra(
                            nameof(StateDownloadState),
                            stateDownloadState.ReceivedIterationCount,
                            stateDownloadState.TotalIterationCount),
                        _ => throw new ExecutionError($"Not supported preload state. {preloadState.GetType()}"),
                    };
                });
            }
        }

        private const int _blockRenderDegreeOfParallelism = 8;

        private BlockHeader? _tipHeader;

        private ISubject<TipChanged> _subject = new ReplaySubject<TipChanged>();

        private StandaloneContext StandaloneContext { get; }

        public StandaloneSubscription(StandaloneContext standaloneContext)
        {
            StandaloneContext = standaloneContext;
            //if (standaloneContext.NineChroniclesNodeService != null)
            //{
            //    AddField(new EventStreamFieldType
            //    {
            //        Name = nameof(AvatarSubscription),
            //        Type= typeof(AvatarSubscription),
            //        Resolver = new FuncFieldResolver<AvatarSubscription>(context => (context.Source as AvatarSubscription)),
            //        Subscriber = new EventStreamResolver<AvatarSubscription>(context => new AvatarSubscription(standaloneContext.NineChroniclesNodeService).)
            //    }
            //        )
            //    Field<NonNullGraphType<AvatarSubscription>>(
            //        name: nameof(AvatarSubscription),
            //        streamres: context => new AvatarSubscription(standaloneContext.NineChroniclesNodeService),
                    
            //        );
            //}
            AddField(new EventStreamFieldType
            {
                Name = "tipChanged",
                Type = typeof(TipChanged),
                Resolver = new FuncFieldResolver<TipChanged>(ResolveTipChanged),
                Subscriber = new EventStreamResolver<TipChanged>(SubscribeTipChanged),
            });
            AddField(new EventStreamFieldType
            {
                Name = "preloadProgress",
                Type = typeof(PreloadStateType),
                Resolver = new FuncFieldResolver<PreloadState>(context => (context.Source as PreloadState)!),
                Subscriber = new EventStreamResolver<PreloadState>(context => StandaloneContext.PreloadStateSubject.AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = "nodeStatus",
                Type = typeof(NodeStatusType),
                Resolver = new FuncFieldResolver<NodeStatusType>(context => (context.Source as NodeStatusType)!),
                Subscriber = new EventStreamResolver<NodeStatusType>(context => StandaloneContext.NodeStatusSubject.AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = "differentAppProtocolVersionEncounter",
                Type = typeof(NonNullGraphType<DifferentAppProtocolVersionEncounterType>),
                Resolver = new FuncFieldResolver<DifferentAppProtocolVersionEncounter>(context =>
                    (DifferentAppProtocolVersionEncounter)context.Source!),
                Subscriber = new EventStreamResolver<DifferentAppProtocolVersionEncounter>(context =>
                    StandaloneContext.DifferentAppProtocolVersionEncounterSubject
                        .Sample(standaloneContext.DifferentAppProtocolVersionEncounterInterval)
                        .AsObservable()
                    ),
            });
            AddField(new EventStreamFieldType
            {
                Name = "notification",
                Type = typeof(NonNullGraphType<NotificationType>),
                Resolver = new FuncFieldResolver<Notification>(context => (context.Source as Notification)!),
                Subscriber = new EventStreamResolver<Notification>(context =>
                    StandaloneContext.NotificationSubject
                        .Sample(standaloneContext.NotificationInterval)
                        .AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = "nodeException",
                Type = typeof(NonNullGraphType<NodeExceptionType>),
                Resolver = new FuncFieldResolver<NodeException>(context => (context.Source as NodeException)!),
                Subscriber = new EventStreamResolver<NodeException>(context =>
                    StandaloneContext.NodeExceptionSubject
                        .Sample(standaloneContext.NodeExceptionInterval)
                        .AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = nameof(MonsterCollectionState),
                Type = typeof(NonNullGraphType<MonsterCollectionStateType>),
                Resolver = new FuncFieldResolver<MonsterCollectionState>(context => (context.Source as MonsterCollectionState)!),
                Subscriber = new EventStreamResolver<MonsterCollectionState>(context =>
                    standaloneContext.MonsterCollectionStateSubject
                        .Sample(standaloneContext.MonsterCollectionStateInterval)
                        .AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = nameof(MonsterCollectionStatus),
                Type = typeof(NonNullGraphType<MonsterCollectionStatusType>),
                Resolver = new FuncFieldResolver<MonsterCollectionStatus>(context => (context.Source as MonsterCollectionStatus)!),
                Subscriber = new EventStreamResolver<MonsterCollectionStatus>(context =>
                    standaloneContext.MonsterCollectionStatusSubject
                        .Sample(standaloneContext.MonsterCollectionStatusInterval)
                        .AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = $"{nameof(MonsterCollectionStatus)}ByAgent",
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "A hex-encoded address of agent.",
                        Name = "address",
                    }
                ),
                Type = typeof(NonNullGraphType<MonsterCollectionStatusType>),
                Resolver = new FuncFieldResolver<MonsterCollectionStatus>(context => (context.Source as MonsterCollectionStatus)!),
                Subscriber = new EventStreamResolver<MonsterCollectionStatus>(SubscribeMonsterCollectionStatus),
            });
            AddField(new EventStreamFieldType
            {
                Name = $"{nameof(MonsterCollectionState)}ByAgent",
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "A hex-encoded address of agent.",
                        Name = "address",
                    }
                ),
                Type = typeof(NonNullGraphType<MonsterCollectionStateType>),
                Resolver = new FuncFieldResolver<MonsterCollectionState>(context => (context.Source as MonsterCollectionState)!),
                Subscriber = new EventStreamResolver<MonsterCollectionState>(SubscribeMonsterCollectionState),
            });
            AddField(new EventStreamFieldType
            {
                Name = "BalanceByAgent",
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "A hex-encoded address of agent.",
                        Name = "address",
                    }
                ),
                Type = typeof(NonNullGraphType<StringGraphType>),
                Resolver = new FuncFieldResolver<string>(context => (string)context.Source!),
                Subscriber = new EventStreamResolver<string>(SubscribeBalance),
            });

            BlockRenderer blockRenderer = standaloneContext.NineChroniclesNodeService!.BlockRenderer;
            blockRenderer.BlockSubject
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderBlock);

            ActionRenderer actionRenderer = standaloneContext.NineChroniclesNodeService!.ActionRenderer;
            actionRenderer.EveryRender<MonsterCollect>()
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderMonsterCollectionStateSubject);
            actionRenderer.EveryRender<CancelMonsterCollect>()
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderMonsterCollectionStateSubject);
            actionRenderer.EveryRender<ClaimMonsterCollectionReward>()
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderMonsterCollectionStateSubject);
            if (standaloneContext.NineChroniclesNodeService != null)
            {
                AvatarSubscription(standaloneContext.NineChroniclesNodeService);
            }
        }

        private IObservable<MonsterCollectionState> SubscribeMonsterCollectionState(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("address");

            StandaloneContext.AgentAddresses.TryAdd(address,
                (new ReplaySubject<MonsterCollectionStatus>(), new ReplaySubject<MonsterCollectionState>(),
                    new ReplaySubject<string>()));
            StandaloneContext.AgentAddresses.TryGetValue(address, out var subjects);
            return subjects.stateSubject.AsObservable();
        }

        private IObservable<MonsterCollectionStatus> SubscribeMonsterCollectionStatus(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("address");

            StandaloneContext.AgentAddresses.TryAdd(address,
                (new ReplaySubject<MonsterCollectionStatus>(), new ReplaySubject<MonsterCollectionState>(),
                    new ReplaySubject<string>()));
            StandaloneContext.AgentAddresses.TryGetValue(address, out var subjects);
            return subjects.statusSubject.AsObservable();
        }

        private IObservable<string> SubscribeBalance(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("address");

            StandaloneContext.AgentAddresses.TryAdd(address,
                (new ReplaySubject<MonsterCollectionStatus>(), new ReplaySubject<MonsterCollectionState>(),
                    new ReplaySubject<string>()));
            StandaloneContext.AgentAddresses.TryGetValue(address, out var subjects);
            return subjects.balanceSubject.AsObservable();
        }

        private TipChanged ResolveTipChanged(IResolveFieldContext context)
        {
            return context.Source as TipChanged ?? throw new InvalidOperationException();
        }

        private IObservable<TipChanged> SubscribeTipChanged(IResolveEventStreamContext context)
        {
            return _subject.AsObservable();
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

            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            Log.Debug("StandaloneSubscription.RenderBlock started");

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
            Log.Debug($"StandaloneSubscription.RenderBlock target addresses. (count: {StandaloneContext.AgentAddresses.Count})");
            StandaloneContext.AgentAddresses
                .AsParallel()
                .WithDegreeOfParallelism(_blockRenderDegreeOfParallelism)
                .ForAll(kv =>
                {
                    Address address = kv.Key;
                    (ReplaySubject<MonsterCollectionStatus> statusSubject, _, ReplaySubject<string> balanceSubject) =
                        kv.Value;
                    RenderForAgent(
                        blockChain,
                        _tipHeader,
                        address,
                        currency,
                        statusSubject,
                        balanceSubject,
                        rewardSheet
                    );
                });

            sw.Stop();
            Log.Debug($"StandaloneSubscription.RenderBlock ended. elapsed: {sw.Elapsed}");
        }

        private void RenderForAgent(
            BlockChain<PolymorphicAction<ActionBase>> blockChain,
            BlockHeader tipHeader,
            Address address,
            Currency currency,
            ReplaySubject<MonsterCollectionStatus> statusSubject,
            ReplaySubject<string> balanceSubject,
            MonsterCollectionRewardSheet rewardSheet)
        {
            FungibleAssetValue agentBalance = blockChain.GetBalance(address, currency, tipHeader.Hash);
            balanceSubject.OnNext(agentBalance.GetQuantityString(true));
            if (blockChain.GetState(address, tipHeader.Hash) is Dictionary rawAgent)
            {
                AgentState agentState = new AgentState(rawAgent);
                Address deriveAddress =
                    MonsterCollectionState.DeriveAddress(address, agentState.MonsterCollectionRound);
                if (agentState.avatarAddresses.Any() &&
                    blockChain.GetState(deriveAddress, tipHeader.Hash) is Dictionary collectDict)
                {
                    var monsterCollectionState = new MonsterCollectionState(collectDict);
                    List<MonsterCollectionRewardSheet.RewardInfo> rewards = monsterCollectionState.CalculateRewards(
                        rewardSheet,
                        tipHeader.Index
                    );

                    var monsterCollectionStatus = new MonsterCollectionStatus(
                        agentBalance,
                        rewards,
                        tipHeader.Index,
                        monsterCollectionState.IsLocked(tipHeader.Index)
                    );
                    statusSubject.OnNext(monsterCollectionStatus);
                }
            }
        }

        private void RenderMonsterCollectionStateSubject<T>(ActionBase.ActionEvaluation<T> eval)
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

            foreach (var (address, subjects) in StandaloneContext.AgentAddresses)
            {
                if (eval.Signer.Equals(address) &&
                    service.BlockChain.GetState(address, _tipHeader?.Hash) is Dictionary agentDict)
                {
                    var agentState = new AgentState(agentDict);
                    Address deriveAddress = MonsterCollectionState.DeriveAddress(address, agentState.MonsterCollectionRound);
                    var subject = subjects.stateSubject;
                    if (eval.OutputStates.GetState(deriveAddress) is Dictionary state)
                    {
                        subject.OnNext(new MonsterCollectionState(state));
                    }
                    else
                    {
                        subject.OnNext(null!);
                    }
                }
            }
        }
    }
}
