using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Subscription;
using GraphQL.Types;
using Lib9c.Renderers;
using Libplanet.Types.Blocks;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Headless;
using Libplanet.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Configuration;
using Nekoyume.Module;
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

        class Tx
        {
            public Transaction? Transaction { get; set; }
            public TxResult? TxResult { get; set; }
        }

        class TxType : ObjectGraphType<Tx>
        {
            public TxType()
            {
                Field<NonNullGraphType<TransactionType>>(nameof(Transaction));
                Field<TxResultType>(nameof(TxResult));
            }
        }

        class PreloadStateType : ObjectGraphType<BlockSyncState>
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
                Field<NonNullGraphType<LongGraphType>>(name: "totalPhase", resolve: context => BlockSyncState.TotalPhase);
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

        private ISubject<TipChanged> _subject = new ReplaySubject<TipChanged>(1);

        private ISubject<Transaction> _transactionSubject = new Subject<Transaction>();

        private StandaloneContext StandaloneContext { get; }

        private IConfiguration Configuration { get; }

        public StandaloneSubscription(StandaloneContext standaloneContext, IConfiguration configuration)
        {
            StandaloneContext = standaloneContext;
            Configuration = configuration;
            if (Convert.ToBoolean(configuration.GetSection("Jwt")["EnableJwtAuthentication"]))
            {
                this.AuthorizeWith(GraphQLService.JwtPolicyKey);
            }

            AddField(new EventStreamFieldType
            {
                Name = "tipChanged",
                Type = typeof(TipChanged),
                Resolver = new FuncFieldResolver<TipChanged>(ResolveTipChanged),
                Subscriber = new EventStreamResolver<TipChanged>(SubscribeTipChanged),
            });
            AddField(new EventStreamFieldType
            {
                Name = "tx",
                Type = typeof(TxType),
                Arguments = new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "A regular expression to filter transactions based on action type.",
                        Name = "actionType",
                    }
                ),
                Resolver = new FuncFieldResolver<Tx>(ResolveTx),
                Subscriber = new EventStreamResolver<Tx>(SubscribeTx),
            });
            AddField(new EventStreamFieldType
            {
                Name = "preloadProgress",
                Type = typeof(PreloadStateType),
                Resolver = new FuncFieldResolver<BlockSyncState>(context => (context.Source as BlockSyncState)!),
                Subscriber = new EventStreamResolver<BlockSyncState>(context => StandaloneContext.PreloadStateSubject.AsObservable()),
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
            actionRenderer.EveryRender<ClaimMonsterCollectionReward>()
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(RenderMonsterCollectionStateSubject);
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

            return Observable.Create<string>(observer =>
            {
                var subscription = subjects.balanceSubject.Subscribe(observer);

                return Disposable.Create(() =>
                {
                    subscription.Dispose();
                    StandaloneContext.AgentAddresses.TryRemove(address, out _);
                });
            });
        }

        private TipChanged ResolveTipChanged(IResolveFieldContext context)
        {
            return context.Source as TipChanged ?? throw new InvalidOperationException();
        }

        private IObservable<TipChanged> SubscribeTipChanged(IResolveEventStreamContext context)
        {
            return _subject.AsObservable();
        }

        private Tx ResolveTx(IResolveFieldContext context)
        {
            return context.Source as Tx ?? throw new InvalidOperationException();
        }

        private IObservable<Tx> SubscribeTx(IResolveFieldContext context)
        {
            var chain = StandaloneContext.BlockChain
                        ?? throw new InvalidOperationException($"{nameof(StandaloneContext.BlockChain)} is null.");
            var store = StandaloneContext.Store
                        ?? throw new InvalidOperationException($"{nameof(StandaloneContext.Store)} is null");
            var actionType = context.GetArgument<string>("actionType");

            return _transactionSubject.AsObservable()
                .Where(tx => tx.Actions.Any(rawAction =>
                {
                    if (rawAction is not Dictionary action || action["type_id"] is not Text typeId)
                    {
                        return false;
                    }

                    return Regex.IsMatch(typeId, actionType);
                }))
                .Select(transaction => new Tx
                {
                    Transaction = transaction,
                    TxResult = GetTxResult(transaction, store, chain),
                });
        }

        private TxResult? GetTxResult(Transaction transaction, IStore store, BlockChain chain)
        {
            if (store.GetFirstTxIdBlockHashIndex(transaction.Id) is not { } blockHash)
            {
                return null;
            }
            var txExecution = store.GetTxExecution(blockHash, transaction.Id);
            var txExecutedBlock = chain[blockHash];
            return new TxResult(
                    txExecution.Fail ? TxStatus.FAILURE : TxStatus.SUCCESS,
                    txExecutedBlock.Index,
                    txExecutedBlock.Hash.ToString(),
                    txExecution.InputState,
                    txExecution.OutputState,
                    txExecution.ExceptionNames);
        }

        private void RenderBlock((Block OldTip, Block NewTip) pair)
        {
            _tipHeader = pair.NewTip.Header;
            _subject.OnNext(
                new TipChanged
                {
                    Index = pair.NewTip.Index,
                    Hash = pair.NewTip.Hash,
                }
            );
            foreach (var transaction in pair.NewTip.Transactions)
            {
                _transactionSubject.OnNext(transaction);
            }

            if (StandaloneContext.NineChroniclesNodeService is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StandaloneContext.NineChroniclesNodeService)} is null.");
            }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            Log.Debug("StandaloneSubscription.RenderBlock started");

            BlockChain blockChain = StandaloneContext.NineChroniclesNodeService.BlockChain;
            Currency currency =
                new GoldCurrencyState(
                    (Dictionary)blockChain.GetWorldState(_tipHeader.Hash).GetLegacyState(Addresses.GoldCurrency)
                ).Currency;
            var rewardSheet = new MonsterCollectionRewardSheet();
            var csv = blockChain.GetWorldState(_tipHeader.Hash)
                .GetLegacyState(Addresses.GetSheetAddress<MonsterCollectionRewardSheet>()).ToDotnetString();
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
            BlockChain blockChain,
            BlockHeader tipHeader,
            Address address,
            Currency currency,
            ReplaySubject<MonsterCollectionStatus> statusSubject,
            ReplaySubject<string> balanceSubject,
            MonsterCollectionRewardSheet rewardSheet)
        {
            FungibleAssetValue agentBalance = blockChain.GetWorldState(tipHeader.Hash).GetBalance(address, currency);
            balanceSubject.OnNext(agentBalance.GetQuantityString(true));
            if (blockChain.GetWorldState(tipHeader.Hash).GetAgentState(address) is { } agentState)
            {
                Address deriveAddress =
                    MonsterCollectionState.DeriveAddress(address, agentState.MonsterCollectionRound);
                if (agentState.avatarAddresses.Any() &&
                    blockChain.GetWorldState(tipHeader.Hash).GetLegacyState(deriveAddress) is Dictionary collectDict)
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

        private void RenderMonsterCollectionStateSubject<T>(ActionEvaluation<T> eval)
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
                    service.BlockChain.GetWorldState(_tipHeader?.Hash).GetAgentState(address) is { } agentState)
                {
                    Address deriveAddress = MonsterCollectionState.DeriveAddress(address, agentState.MonsterCollectionRound);
                    var subject = subjects.stateSubject;
                    if (service.BlockChain.GetAccountState(eval.OutputState).GetState(deriveAddress) is Dictionary state)
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
