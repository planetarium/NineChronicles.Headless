using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using Lib9c.Renderer;
using Libplanet.Action;
using Libplanet.Assets;
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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class GraphQLTestBase
    {
        protected ITestOutputHelper _output;

        protected readonly IHttpContextAccessor _httpContextAccessor;

        public GraphQLTestBase(ITestOutputHelper output)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            _output = output;
            _httpContextAccessor = new HttpContextAccessor();

            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(
                new DefaultKeyValueStore(null),
                new DefaultKeyValueStore(null)
            );
            var goldCurrency = new Currency("NCG", 2, minter: null);

            var fixturePath = Path.Combine("..", "..", "..", "..", "Lib9c", ".Lib9c.Tests", "Data", "TableCSV");
            var sheets = TableSheetsImporter.ImportSheets(fixturePath);
            var blockAction = new RewardGold();
            var genesisBlock = BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                new PolymorphicAction<ActionBase>[]
                {
                    new InitializeStates(
                        rankingState: new RankingState(),
                        shopState: new ShopState(),
                        gameConfigState: new GameConfigState(sheets[nameof(GameConfigSheet)]),
                        redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                            .Add("address", RedeemCodeState.Address.Serialize())
                            .Add("map", Bencodex.Types.Dictionary.Empty)
                        ),
                        adminAddressState: new AdminState(default, 0),
                        activatedAccountsState: new ActivatedAccountsState(),
                        goldCurrencyState: new GoldCurrencyState(goldCurrency),
                        goldDistributions: new GoldDistribution[]{ },
                        tableSheets: sheets,
                        pendingActivationStates: new PendingActivationState[]{ }
                    ),
                }, blockAction: blockAction);

            var blockPolicy = new BlockPolicy<PolymorphicAction<ActionBase>>(blockAction: blockAction);
            var blockChain = new BlockChain<PolymorphicAction<ActionBase>>(
                blockPolicy,
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
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

            var services = new ServiceCollection();
            services.AddSingleton(StandaloneContextFx);
            services.AddSingleton(StandaloneContextFx.KeyStore ?? GraphQLTestUtils.CreateRandomWeb3KeyStore());
            services.AddSingleton(_httpContextAccessor);
            services.AddSingleton<IConfiguration>(configuration);
            services.AddGraphTypes();
            services.AddLibplanetExplorer<NCAction>();
            services.AddSingleton<StateQuery>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Schema = new StandaloneSchema(serviceProvider);
            Schema.Subscription.As<StandaloneSubscription>().RegisterTipChangedSubscription();

            DocumentExecutor = new DocumentExecuter();
        }

        protected StandaloneSchema Schema { get; }

        protected StandaloneContext StandaloneContextFx { get; }

        protected BlockChain<PolymorphicAction<ActionBase>> BlockChain =>
            StandaloneContextFx.BlockChain!;

        protected IKeyStore KeyStore =>
            StandaloneContextFx.KeyStore!;

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
            Progress<PreloadState>? preloadProgress = null,
            IEnumerable<Peer>? peers = null,
            IEnumerable<Peer>? staticPeers = null)
            where T : IAction, new()
        {
            var properties = new LibplanetNodeServiceProperties<T>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = appProtocolVersion,
                GenesisBlock = genesisBlock,
                StoreStatesCacheSize = 2,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                SwarmPrivateKey = new PrivateKey(),
                Port = null,
                MinimumDifficulty = 1024,
                NoMiner = true,
                Render = false,
                Peers = peers ?? ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = ImmutableHashSet<PublicKey>.Empty.Add(appProtocolVersionSigner),
                StaticPeers = staticPeers ?? ImmutableHashSet<Peer>.Empty,
            };

            return new LibplanetNodeService<T>(
                properties,
                blockPolicy: new BlockPolicy<T>(),
                stagePolicy: new VolatileStagePolicy<T>(),
                renderers: new[] { new DummyRenderer<T>() },
                minerLoopAction: (chain, swarm, privateKey, _) => Task.CompletedTask,
                preloadProgress: preloadProgress,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { }
            );
        }
    }
}
