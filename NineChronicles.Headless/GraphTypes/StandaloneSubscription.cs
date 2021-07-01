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
using Serilog;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneSubscription : ObjectGraphType
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
            private class PreloadStateExtra
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

            private class PreloadStateExtraType : ObjectGraphType<PreloadStateExtra>
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

        private ISubject<TipChanged> _subject = new ReplaySubject<TipChanged>();

        private StandaloneContext StandaloneContext { get; }

        public StandaloneSubscription(StandaloneContext standaloneContext)
        {
            StandaloneContext = standaloneContext;
            AddField(new EventStreamFieldType {
                Name = "tipChanged",
                Type = typeof(TipChanged),
                Resolver = new FuncFieldResolver<TipChanged>(ResolveTipChanged),
                Subscriber = new EventStreamResolver<TipChanged>(SubscribeTipChanged),
            });
            AddField(new EventStreamFieldType {
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
                    (DifferentAppProtocolVersionEncounter)context.Source),
                Subscriber = new EventStreamResolver<DifferentAppProtocolVersionEncounter>(context =>
                    StandaloneContext.DifferentAppProtocolVersionEncounterSubject.AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = "notification",
                Type = typeof(NonNullGraphType<NotificationType>),
                Resolver = new FuncFieldResolver<Notification>(context => (context.Source as Notification)!),
                Subscriber = new EventStreamResolver<Notification>(context =>
                    StandaloneContext.NotificationSubject.AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = "nodeException",
                Type = typeof(NonNullGraphType<NodeExceptionType>),
                Resolver = new FuncFieldResolver<NodeException>(context => (context.Source as NodeException)!),
                Subscriber = new EventStreamResolver<NodeException>(context =>
                    StandaloneContext.NodeExceptionSubject.AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = nameof(MonsterCollectionState),
                Type = typeof(NonNullGraphType<MonsterCollectionStateType>),
                Resolver = new FuncFieldResolver<MonsterCollectionState>(context => (context.Source as MonsterCollectionState)!),
                Subscriber = new EventStreamResolver<MonsterCollectionState>(context => standaloneContext.MonsterCollectionStateSubject.AsObservable()),
            });
            AddField(new EventStreamFieldType
            {
                Name = nameof(MonsterCollectionStatus),
                Type = typeof(NonNullGraphType<MonsterCollectionStatusType>),
                Resolver = new FuncFieldResolver<MonsterCollectionStatus>(context => (context.Source as MonsterCollectionStatus)!),
                Subscriber = new EventStreamResolver<MonsterCollectionStatus>(context => standaloneContext.MonsterCollectionStatusSubject.AsObservable()),
            });

            BlockRenderer blockRenderer = standaloneContext.NineChroniclesNodeService!.BlockRenderer;
            blockRenderer.EveryBlock().Subscribe(RenderBlock);

            ActionRenderer actionRenderer = standaloneContext.NineChroniclesNodeService!.ActionRenderer;
            actionRenderer.EveryRender<ActionBase>().Subscribe(RenderAction);
            actionRenderer.EveryRender<MonsterCollect>().Subscribe(RenderMonsterCollectionStateSubject);
            actionRenderer.EveryRender<CancelMonsterCollect>().Subscribe(RenderMonsterCollectionStateSubject);
            actionRenderer.EveryRender<ClaimMonsterCollectionReward>().Subscribe(RenderMonsterCollectionStateSubject);
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
            _subject.OnNext(new TipChanged
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

            if (StandaloneContext.NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            Libplanet.Blockchain.BlockChain<PolymorphicAction<ActionBase>> blockChain = 
                StandaloneContext.NineChroniclesNodeService.BlockChain;
            Address agentAddress = StandaloneContext.NineChroniclesNodeService.MinerPrivateKey.ToAddress();
            Currency currency =
                new GoldCurrencyState(
                    (Dictionary) blockChain.GetState(Addresses.GoldCurrency)
                ).Currency;
            FungibleAssetValue balance = blockChain.GetBalance(agentAddress, currency);
            var rewards = new List<MonsterCollectionRewardSheet.RewardInfo>();
            if (blockChain.GetState(agentAddress) is Dictionary agentDict)
            {
                AgentState agentState = new AgentState(agentDict);
                Address deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                if (blockChain.GetState(deriveAddress) is Dictionary collectDict && 
                    agentState.avatarAddresses.Any())
                {
                    var rewardSheet = new MonsterCollectionRewardSheet();
                    var csv = blockChain.GetState(
                        Addresses.GetSheetAddress<MonsterCollectionRewardSheet>()
                    ).ToDotnetString();
                    rewardSheet.Set(csv);
                    long tipIndex = blockChain.Tip.Index;
                    var monsterCollectionState = new MonsterCollectionState(collectDict);
                    rewards = monsterCollectionState.CalculateRewards(
                        rewardSheet,
                        tipIndex
                    );

                    var monsterCollectionStatus = new MonsterCollectionStatus(
                        balance, 
                        rewards,
                        monsterCollectionState.IsLocked(tipIndex)
                    );
                    StandaloneContext.MonsterCollectionStatusSubject.OnNext(monsterCollectionStatus);
                }
            }
        }

        private void RenderAction(ActionBase.ActionEvaluation<ActionBase> eval)
        {
            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            if (StandaloneContext.NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
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

            if (!(service.MinerPrivateKey is { } privateKey))
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            Address agentAddress = privateKey.ToAddress();
            if (eval.Signer == agentAddress && eval.Exception is null)
            {
                var agentState = new AgentState((Dictionary)service.BlockChain.GetState(agentAddress));
                Address deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                if (eval.OutputStates.GetState(deriveAddress) is { } state)
                {
                    MonsterCollectionState monsterCollectionState = new MonsterCollectionState((Dictionary)state);
                    StandaloneContext.MonsterCollectionStateSubject.OnNext(monsterCollectionState);
                }
            }

        }
    }
}
