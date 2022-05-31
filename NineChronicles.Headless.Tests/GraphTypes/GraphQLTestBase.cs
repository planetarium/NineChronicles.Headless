using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.KeyStore;
using Libplanet.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Tests.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Lib9c.Tests;
using Xunit.Abstractions;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class GraphQLTestBase
    {
        protected ITestOutputHelper _output;

        public GraphQLTestBase(ITestOutputHelper output)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            _output = output;

            var goldCurrency = new Currency("NCG", 2, minter: null);

            var sheets =
                TableSheetsImporter.ImportSheets(Path.Join("..", "..", "..", "..", "Lib9c", "Lib9c", "TableCSV"));
            var blockAction = new RewardGold();
            var genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>(),
                new NCAction[]
                {
                    new InitializeStates(
                        rankingState: new RankingState0(),
                        shopState: new ShopState(),
                        gameConfigState: new GameConfigState(sheets[nameof(GameConfigSheet)]),
                        redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                            .Add("address", RedeemCodeState.Address.Serialize())
                            .Add("map", Bencodex.Types.Dictionary.Empty)
                        ),
                        adminAddressState: new AdminState(AdminAddress, 10000),
                        activatedAccountsState: new ActivatedAccountsState(),
                        goldCurrencyState: new GoldCurrencyState(goldCurrency),
                        goldDistributions: new GoldDistribution[]{ },
                        tableSheets: sheets,
                        pendingActivationStates: new PendingActivationState[]{ }
                    ),
                }, blockAction: blockAction);

            var ncService = ServiceBuilder.CreateNineChroniclesNodeService(genesisBlock, new PrivateKey());
            var tempKeyStorePath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
            var keyStore = new Web3KeyStore(tempKeyStorePath);

            StandaloneContextFx = new StandaloneContext
            {
                KeyStore = keyStore,
            };
            ncService.ConfigureContext(StandaloneContextFx);

            var configurationBuilder = new ConfigurationBuilder();
            var configuration = configurationBuilder.Build();

            var services = new ServiceCollection();
            var publisher = new ActionEvaluationPublisher(
                ncService.BlockRenderer,
                ncService.ActionRenderer,
                ncService.ExceptionRenderer,
                ncService.NodeStatusRenderer,
                "",
                0,
                new RpcContext()
            );
            services.AddSingleton(publisher);
            services.AddSingleton(StandaloneContextFx);
            services.AddSingleton<IConfiguration>(configuration);
            services.AddGraphTypes();
            services.AddLibplanetExplorer<NCAction>();
            services.AddSingleton<StateQuery>();
            services.AddSingleton(ncService);
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Schema = new StandaloneSchema(serviceProvider);

            DocumentExecutor = new DocumentExecuter();
        }

        protected PrivateKey AdminPrivateKey { get; } = new PrivateKey();

        protected Address AdminAddress => AdminPrivateKey.ToAddress();

        protected StandaloneSchema Schema { get; }

        protected StandaloneContext StandaloneContextFx { get; }

        protected BlockChain<NCAction> BlockChain =>
            StandaloneContextFx.BlockChain!;

        protected IKeyStore KeyStore =>
            StandaloneContextFx.KeyStore!;

        protected IDocumentExecuter DocumentExecutor { get; }
        
        protected SubscriptionDocumentExecuter SubscriptionDocumentExecuter { get; } = new SubscriptionDocumentExecuter();

        protected Task<ExecutionResult> ExecuteQueryAsync(string query)
        {
            return DocumentExecutor.ExecuteAsync(new ExecutionOptions
            {
                Query = query,
                Schema = Schema,
            });
        }
        protected Task<ExecutionResult> ExecuteSubscriptionQueryAsync(string query)
        {
            return SubscriptionDocumentExecuter.ExecuteAsync(new ExecutionOptions
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
            ImmutableHashSet<BoundPeer>? staticPeers = null)
            where T : IAction, new()
        {
            var consensusPrivateKey = new PrivateKey();
            
            var properties = new LibplanetNodeServiceProperties<T>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = appProtocolVersion,
                GenesisBlock = genesisBlock,
                StoreStatesCacheSize = 2,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                SwarmPrivateKey = new PrivateKey(),
                ConsensusPrivateKey = consensusPrivateKey,
                ConsensusPort = 5000,
                NodeId = 0,
                Validators = new List<PublicKey>()
                {
                    consensusPrivateKey.PublicKey,
                },
                Port = null,
                NoMiner = true,
                Render = false,
                Peers = peers ?? ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = ImmutableHashSet<PublicKey>.Empty.Add(appProtocolVersionSigner),
                StaticPeers = staticPeers ?? ImmutableHashSet<BoundPeer>.Empty,
            };

            return new LibplanetNodeService<T>(
                properties,
                blockPolicy: new BlockPolicy<T>(),
                stagePolicy: new VolatileStagePolicy<T>(),
                renderers: new[] { new DummyRenderer<T>() },
                preloadProgress: preloadProgress,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { }
            );
        }
    }
}
