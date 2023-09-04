using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Model.State;
using Nekoyume.Module;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless.Tests
{
    public static class GraphQLTestUtils
    {
        private static readonly IActionLoader _actionLoader = new NCActionLoader();

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
                _ => policy.BlockAction,
                new BlockChainStates(store, stateStore),
                new NCActionLoader());
            var genesisBlock = BlockChain.ProposeGenesisBlock(actionEvaluator);
            var blockchain = BlockChain.Create(
                new BlockPolicy(),
                new VolatileStagePolicy(),
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            var currencyFactory = new CurrencyFactory(blockchain.GetWorldState);
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
            InitializeStates initializeStates,
            PrivateKey minerPrivateKey
        )
        {
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var policy = new BlockPolicy();
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                new BlockChainStates(store, stateStore),
                new NCActionLoader());
            var genesisBlock = BlockChain.ProposeGenesisBlock(
                actionEvaluator,
                transactions: ImmutableList<Transaction>.Empty.Add(Transaction.Create(
                    0, minerPrivateKey, null, new ActionBase[]
                    {
                        initializeStates,
                    }.ToPlainValues())),
                privateKey: minerPrivateKey
            );
            var blockchain = BlockChain.Create(
                new BlockPolicy(),
                new VolatileStagePolicy(),
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            var ncg = new GoldCurrencyState(
                (Dictionary)LegacyModule.GetState(
                    blockchain.GetWorldState(),
                    Addresses.GoldCurrency)).Currency;
            var currencyFactory = new CurrencyFactory(blockchain.GetWorldState, ncg);
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
