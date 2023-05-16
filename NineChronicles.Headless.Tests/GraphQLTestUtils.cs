using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tx;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests
{
    public static class GraphQLTestUtils
    {
        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            StandaloneContext? standaloneContext = null)
            where TObjectGraphType : class, IObjectGraphType
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(TObjectGraphType));
            if (!(standaloneContext is null))
            {
                services.AddSingleton(standaloneContext);
            }

            services.AddLibplanetExplorer<NCAction>();

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

        public static NCAction DeserializeNCAction(IValue value)
        {
#pragma warning disable CS0612
            NCAction action = new NCAction();
#pragma warning restore CS0612
            action.LoadPlainValue(value);
            return action;
        }

        public static StandaloneContext CreateStandaloneContext()
        {
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var genesisBlock = BlockChain<NCAction>.ProposeGenesisBlock();
            var policy = new BlockPolicy<NCAction>();
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                new BlockChainStates(store, stateStore),
                genesisBlock.Hash,
                policy.NativeTokens.Contains,
                new StaticActionLoader(new[] { typeof(ActionBase).Assembly }),
                null);
            var blockchain = BlockChain<PolymorphicAction<ActionBase>>.Create(
                new BlockPolicy<PolymorphicAction<ActionBase>>(),
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            return new StandaloneContext
            {
                BlockChain = blockchain,
                Store = store,
            };
        }

        public static StandaloneContext CreateStandaloneContext(
            InitializeStates initializeStates,
            PrivateKey minerPrivateKey
        )
        {
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var genesisBlock = BlockChain<NCAction>.ProposeGenesisBlock(
                transactions: ImmutableList<Transaction>.Empty.Add(Transaction.Create(
                    0, minerPrivateKey, null, new NCAction[]
                    {
                        initializeStates,
                    })),
                privateKey: minerPrivateKey
            );
            var policy = new BlockPolicy<NCAction>();
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                new BlockChainStates(store, stateStore),
                genesisBlock.Hash,
                policy.NativeTokens.Contains,
                new StaticActionLoader(new[] { typeof(ActionBase).Assembly }),
                null);
            var blockchain = BlockChain<PolymorphicAction<ActionBase>>.Create(
                new BlockPolicy<PolymorphicAction<ActionBase>>(),
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            return new StandaloneContext
            {
                BlockChain = blockchain,
                Store = store,
            };
        }
    }
}
