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
using System.Collections.Concurrent;
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
using Libplanet.Blockchain;
using Libplanet.Blockchain.Renderers;

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

            BlockRenderer blockRenderer = standaloneContext.NineChroniclesNodeService!.BlockRenderer;
            blockRenderer.EveryBlock().Subscribe(RenderBlock);

            ActionRenderer actionRenderer = standaloneContext.NineChroniclesNodeService!.ActionRenderer;
            actionRenderer.EveryRender<ActionBase>().Subscribe(RenderAction);
            actionRenderer.EveryRender<MonsterCollect>().Subscribe(RenderMonsterCollectionStateSubject);
            actionRenderer.EveryRender<CancelMonsterCollect>().Subscribe(RenderMonsterCollectionStateSubject);
            actionRenderer.EveryRender<ClaimMonsterCollectionReward>().Subscribe(RenderMonsterCollectionStateSubject);
        }

        private IObservable<MonsterCollectionState> SubscribeMonsterCollectionState(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("address");

            StandaloneContext.AgentAddresses.TryAdd(address, (new ReplaySubject<MonsterCollectionStatus>(), new ReplaySubject<MonsterCollectionState>()));
            StandaloneContext.AgentAddresses.TryGetValue(address, out var subjects);
            return subjects.stateSubject.AsObservable();
        }

        private IObservable<MonsterCollectionStatus> SubscribeMonsterCollectionStatus(IResolveEventStreamContext context)
        {
            var address = context.GetArgument<Address>("address");

            StandaloneContext.AgentAddresses.TryAdd(address, (new ReplaySubject<MonsterCollectionStatus>(), new ReplaySubject<MonsterCollectionState>()));
            StandaloneContext.AgentAddresses.TryGetValue(address, out var subjects);
            return subjects.statusSubject.AsObservable();
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

            BlockChain<PolymorphicAction<ActionBase>> blockChain = StandaloneContext.NineChroniclesNodeService.BlockChain;
            DelayedRenderer<PolymorphicAction<ActionBase>>? delayedRenderer = blockChain.GetDelayedRenderer();
            BlockHash? offset = delayedRenderer?.Tip?.Hash;
            Currency currency =
                new GoldCurrencyState(
                    (Dictionary) blockChain.GetState(Addresses.GoldCurrency, offset)
                ).Currency;
            var rewardSheet = new MonsterCollectionRewardSheet();
            var csv = blockChain.GetState(
                Addresses.GetSheetAddress<MonsterCollectionRewardSheet>(),
                offset
            ).ToDotnetString();
            rewardSheet.Set(csv);
            foreach (var (address, subjects) in StandaloneContext.AgentAddresses)
            {
                FungibleAssetValue agentBalance = blockChain.GetBalance(address, currency, offset);
                if (blockChain.GetState(address, offset) is Dictionary rawAgent)
                {
                    AgentState agentState = new AgentState(rawAgent);
                    Address deriveAddress =
                        MonsterCollectionState.DeriveAddress(address, agentState.MonsterCollectionRound);
                    if (blockChain.GetState(deriveAddress, offset) is Dictionary collectDict &&
                        agentState.avatarAddresses.Any())
                    {
                        long tipIndex = blockChain.Tip.Index;
                        var monsterCollectionState = new MonsterCollectionState(collectDict);
                        List<MonsterCollectionRewardSheet.RewardInfo> rewards = monsterCollectionState.CalculateRewards(
                            rewardSheet,
                            tipIndex
                        );

                        var monsterCollectionStatus = new MonsterCollectionStatus(
                            agentBalance,
                            rewards,
                            monsterCollectionState.IsLocked(tipIndex)
                        );
                        subjects.statusSubject.OnNext(monsterCollectionStatus);
                    }
                }
            }

            if (StandaloneContext.NineChroniclesNodeService.MinerPrivateKey is null)
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            // legacy
            Address agentAddress = StandaloneContext.NineChroniclesNodeService.MinerPrivateKey.ToAddress();
            FungibleAssetValue balance = blockChain.GetBalance(agentAddress, currency, offset);
            if (blockChain.GetState(agentAddress, offset) is Dictionary agentDict)
            {
                AgentState agentState = new AgentState(agentDict);
                Address deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                if (blockChain.GetState(deriveAddress, offset) is Dictionary collectDict && 
                    agentState.avatarAddresses.Any())
                {
                    rewardSheet.Set(csv);
                    long tipIndex = blockChain.Tip.Index;
                    var monsterCollectionState = new MonsterCollectionState(collectDict);
                    var rewards = monsterCollectionState.CalculateRewards(
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

            foreach (var (address, subjects) in StandaloneContext.AgentAddresses)
            {
                if (eval.Signer.Equals(address) &&
                    eval.Exception is null &&
                    service.BlockChain.GetState(address) is Dictionary agentDict)
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

            // legacy
            if (!(service.MinerPrivateKey is { } privateKey))
            {
                Log.Information("PrivateKey is not set. please call SetPrivateKey() first");
                return;
            }

            Address agentAddress = privateKey.ToAddress();
            if (eval.Signer == agentAddress && 
                eval.Exception is null && 
                service.BlockChain.GetState(agentAddress) is Dictionary rawAgent)
            {
                var agentState = new AgentState(rawAgent);
                Address deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                if (eval.OutputStates.GetState(deriveAddress) is Dictionary state)
                {
                    StandaloneContext.MonsterCollectionStateSubject.OnNext(
                        new MonsterCollectionState(state)
                    );
                }
                else
                {
                    StandaloneContext.MonsterCollectionStateSubject.OnNext(null!);
                }
            }

        }
    }
}
