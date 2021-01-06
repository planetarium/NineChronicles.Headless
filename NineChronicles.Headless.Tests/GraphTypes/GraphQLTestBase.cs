using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using Lib9c.Renderer;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Libplanet.Store;
using Microsoft.Extensions.Configuration;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using Serilog;
using Xunit.Abstractions;
using RewardGold = NineChronicles.Headless.Tests.Common.Actions.RewardGold;
using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class GraphQLTestBase
    {
        protected ITestOutputHelper _output;

        public GraphQLTestBase(ITestOutputHelper output)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            _output = output;

            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(
                new DefaultKeyValueStore(null),
                new DefaultKeyValueStore(null)
            );
            var genesisBlock = BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(blockAction: new RewardGold());

            var blockPolicy = new BlockPolicy<PolymorphicAction<ActionBase>>(blockAction: new RewardGold());
            var blockChain = new BlockChain<PolymorphicAction<ActionBase>>(
                blockPolicy,
                store,
                stateStore,
                genesisBlock,
                renderers: new IRenderer<PolymorphicAction<ActionBase>>[] { new BlockRenderer(), new ActionRenderer() }
            );

            var tempKeyStorePath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
            var keyStore = new Web3KeyStore(tempKeyStorePath);

            StandaloneContextFx = new StandaloneContext
            {
                BlockChain = blockChain,
                KeyStore = keyStore,
            };

            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();
            Schema = new StandaloneSchema(new TestServiceProvider(StandaloneContextFx, configuration));
            Schema.Subscription.As<StandaloneSubscription>().RegisterTipChangedSubscription();

            DocumentExecutor = new DocumentExecuter();
        }

        protected StandaloneSchema Schema { get; }

        protected StandaloneContext StandaloneContextFx { get; }

        protected BlockChain<PolymorphicAction<ActionBase>> BlockChain =>
            StandaloneContextFx.BlockChain;

        protected IKeyStore KeyStore =>
            StandaloneContextFx.KeyStore;

        protected IDocumentExecuter DocumentExecutor { get; }

        protected Task<ExecutionResult> ExecuteQueryAsync(string query)
        {
            return DocumentExecutor.ExecuteAsync(new ExecutionOptions
            {
                Query = query,
                Schema = Schema,
            });
        }

        protected async Task<Task> StartAsync<T>(
            Swarm<T> swarm,
            CancellationToken cancellationToken = default
        )
            where T : IAction, new()
        {
            Task task = swarm.StartAsync(
              millisecondsDialTimeout: 200,
              millisecondsBroadcastTxInterval: 200,
              cancellationToken: cancellationToken
            );
            await swarm.WaitForRunningAsync();
            return task;
        }

        protected LibplanetNodeService<T> CreateLibplanetNodeService<T>(
            Block<T> genesisBlock,
            AppProtocolVersion appProtocolVersion,
            PublicKey appProtocolVersionSigner,
            Progress<PreloadState> preloadProgress = null,
            IEnumerable<Peer> peers = null)
            where T : IAction, new()
        {
            var properties = new LibplanetNodeServiceProperties<T>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = appProtocolVersion,
                GenesisBlock = genesisBlock,
                StoreStatesCacheSize = 2,
                PrivateKey = new PrivateKey(),
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                Port = null,
                MinimumDifficulty = 1024,
                NoMiner = true,
                Render = false,
                Peers = peers ?? ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = ImmutableHashSet<PublicKey>.Empty.Add(appProtocolVersionSigner),
            };

            return new LibplanetNodeService<T>(
                properties,
                blockPolicy: new BlockPolicy<T>(),
                renderers: new[] { new DummyRenderer<T>() },
                minerLoopAction: (chain, swarm, privateKey, _) => Task.CompletedTask,
                preloadProgress: preloadProgress,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { }
            );
        }

        private class TestServiceProvider : IServiceProvider
        {
            private StandaloneQuery Query;

            private StandaloneMutation Mutation;

            private StandaloneSubscription Subscription;

            private StandaloneContext StandaloneContext;

            public TestServiceProvider(StandaloneContext standaloneContext, IConfiguration configuration)
            {
                Query = new StandaloneQuery(standaloneContext);
                Mutation = new StandaloneMutation(standaloneContext, configuration);
                Subscription = new StandaloneSubscription(standaloneContext);
                StandaloneContext = standaloneContext;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(StandaloneQuery))
                {
                    return Query;
                }

                if (serviceType == typeof(StandaloneMutation))
                {
                    return Mutation;
                }

                if (serviceType == typeof(StandaloneSubscription))
                {
                    return Subscription;
                }

                if (serviceType == typeof(ValidationQuery))
                {
                    return new ValidationQuery(StandaloneContext);
                }

                if (serviceType == typeof(ActivationStatusQuery))
                {
                    return new ActivationStatusQuery(StandaloneContext);
                }

                if (serviceType == typeof(PeerChainStateQuery))
                {
                    return new PeerChainStateQuery(StandaloneContext);
                }

                return Activator.CreateInstance(serviceType);
            }
        }
    }
}
