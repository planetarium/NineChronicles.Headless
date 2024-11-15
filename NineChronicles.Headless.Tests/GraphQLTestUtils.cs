using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Model.State;
using Nekoyume.Module;
using NineChronicles.Headless.Repositories.BlockChain;
using NineChronicles.Headless.Repositories.StateTrie;
using NineChronicles.Headless.Repositories.Transaction;
using NineChronicles.Headless.Repositories.WorldState;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless.Tests
{
    public static class GraphQLTestUtils
    {
        private static readonly IActionLoader _actionLoader = new NCActionLoader();
        private static readonly List<PrivateKey> ValidatorPrivateKeys = new List<PrivateKey>
        {
            PrivateKey.FromString(
                "e5792a1518d9c7f7ecc35cd352899211a05164c9dde059c9811e0654860549ef"),
            PrivateKey.FromString(
                "91d61834be824c952754510fcf545180eca38e036d3d9b66564f0667b30d5b93"),
            PrivateKey.FromString(
                "b17c919b07320edfb3e6da2f1cfed75910322de2e49377d6d4d226505afca550"),
            PrivateKey.FromString(
                "91602d7091c5c7837ac8e71a8d6b1ed1355cfe311914d9a76107899add0ad56a"),
        };

        public static PrivateKey MinerPrivateKey => ValidatorPrivateKeys.First();

        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            StandaloneContext? standaloneContext = null)
            where TObjectGraphType : class, IObjectGraphType
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(TObjectGraphType));
            if (standaloneContext is not null)
            {
                services.AddSingleton(standaloneContext);
            }

            services.AddLibplanetExplorer();
            services.AddSingleton<StateMemoryCache>();
            services.AddTransient<IWorldStateRepository, WorldStateRepository>();
            services.AddTransient<IBlockChainRepository, BlockChainRepository>();
            services.AddSingleton<IStateTrieRepository, StateTrieRepository>();
            services.AddSingleton<ITransactionRepository, TransactionRepository>();

            var serviceProvider = services.BuildServiceProvider();
            return ExecuteQueryAsync<TObjectGraphType>(
                serviceProvider,
                query,
                userContext,
                source);
        }

        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            IServiceProvider serviceProvider,
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null)
            where TObjectGraphType : IObjectGraphType
        {
            var graphType = (IObjectGraphType)serviceProvider.GetService(typeof(TObjectGraphType))!;
            return ExecuteQueryAsync(graphType, query, userContext, source);
        }

        public static Task<ExecutionResult> ExecuteQueryAsync(
            IObjectGraphType graphType,
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null)
        {
            var documentExecutor = new DocumentExecuter();
            return documentExecutor.ExecuteAsync(new ExecutionOptions
            {
                Query = query,
                Schema = new Schema
                {
                    Query = graphType,
                },
                UserContext = userContext!,
                Root = source,
            });
        }

        // FIXME: Passing 0 index is bad.
        public static ActionBase DeserializeNCAction(IValue value) =>
            (ActionBase)_actionLoader.LoadAction(0, value);

        public static StandaloneContext CreateStandaloneContext()
        {
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var policy = new BlockPolicy();
            var actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: policy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            var genesisBlock = BlockChain.ProposeGenesisBlock();
            var blockchain = BlockChain.Create(
                new BlockPolicy(),
                new VolatileStagePolicy(),
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            var currencyFactory = new CurrencyFactory(() => blockchain.GetWorldState(blockchain.Tip.Hash));
            var fungibleAssetValueFactory = new FungibleAssetValueFactory(currencyFactory);
            return new StandaloneContext
            {
                BlockChain = blockchain,
                Store = store,
                CurrencyFactory = currencyFactory,
                FungibleAssetValueFactory = fungibleAssetValueFactory,
            };
        }

        public static StandaloneContext CreateStandaloneContext(
            InitializeStates initializeStates
        )
        {
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var policy = new BlockPolicy();
            var actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: policy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            var genesisBlock = BlockChain.ProposeGenesisBlock(
                transactions: ImmutableList<Transaction>.Empty
                .Add(
                    Transaction.Create(
                        0, MinerPrivateKey,
                        null,
                        new IAction[]
                        {
                            new Initialize(
                                new ValidatorSet(
                                    ValidatorPrivateKeys.Select(
                                        v => new Validator(v.PublicKey, BigInteger.One)).ToList()),
                                ImmutableDictionary.Create<Address, IValue>())
                        }.Select(a => a.PlainValue)))
                .Add(
                    Transaction.Create(
                        1,
                        MinerPrivateKey,
                        null,
                        new ActionBase[]
                        {
                            initializeStates,
                        }.ToPlainValues())),
                privateKey: MinerPrivateKey
            );
            var blockchain = BlockChain.Create(
                new BlockPolicy(),
                new VolatileStagePolicy(),
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            var block = blockchain.ProposeBlock(MinerPrivateKey, null);
            var blockCommit = new BlockCommit(
                block.Index,
                0,
                block.Hash,
                ValidatorPrivateKeys.Select(
                    k => new VoteMetadata(block.Index, 0, block.Hash, block.Timestamp, k.PublicKey, BigInteger.One, VoteFlag.PreCommit).Sign(k))
                .ToImmutableArray());

            blockchain.Append(block, blockCommit);
            var ncg = new GoldCurrencyState((Dictionary)blockchain.GetWorldState().GetLegacyState(Addresses.GoldCurrency))
                .Currency;
            var currencyFactory = new CurrencyFactory(() => blockchain.GetWorldState(blockchain.Tip.Hash), ncg);
            var fungibleAssetValueFactory = new FungibleAssetValueFactory(currencyFactory);
            return new StandaloneContext
            {
                BlockChain = blockchain,
                Store = store,
                CurrencyFactory = currencyFactory,
                FungibleAssetValueFactory = fungibleAssetValueFactory,
            };
        }
    }
}
