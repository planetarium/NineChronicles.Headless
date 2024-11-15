using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Types.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.Blockchain.Policy;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Tests.Common;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Mocks;
using Libplanet.Types.Tx;
using Moq;
using NineChronicles.Headless.Executable.Tests.KeyStore;
using NineChronicles.Headless.Repositories;
using NineChronicles.Headless.Repositories.BlockChain;
using NineChronicles.Headless.Repositories.StateTrie;
using NineChronicles.Headless.Repositories.Transaction;
using NineChronicles.Headless.Repositories.WorldState;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class GraphQLTestBase
    {
        protected ITestOutputHelper _output;

        public GraphQLTestBase(ITestOutputHelper output)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            _output = output;

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618

            var sheets = TableSheetsImporter.ImportSheets();
            var genesisBlock = BlockChain.ProposeGenesisBlock(
                transactions: ImmutableList<Transaction>.Empty.Add(Transaction.Create(0,
                    AdminPrivateKey, null, new ActionBase[]
                    {
                        new InitializeStates(
                            validatorSet: new ValidatorSet(new List<Validator>
                            {
                                new Validator(ProposerPrivateKey.PublicKey, 10_000_000_000_000_000_000)
                            }),
                            rankingState: new RankingState0(),
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(sheets[nameof(GameConfigSheet)]),
                            redeemCodeState: new RedeemCodeState(
                                Bencodex.Types.Dictionary.Empty
                                    .Add("address", RedeemCodeState.Address.Serialize())
                                    .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(AdminAddress, 10000),
                            activatedAccountsState: new ActivatedAccountsState(),
                            goldCurrencyState: new GoldCurrencyState(goldCurrency),
                            goldDistributions: new GoldDistribution[] { },
                            tableSheets: sheets,
                            pendingActivationStates: new PendingActivationState[] { }
                        ),
                    }.ToPlainValues())),
                privateKey: AdminPrivateKey);

            var ncService = ServiceBuilder.CreateNineChroniclesNodeService(genesisBlock, ProposerPrivateKey);

            StandaloneContextFx = new StandaloneContext
            {
                KeyStore = KeyStore,
                DifferentAppProtocolVersionEncounterInterval = TimeSpan.FromSeconds(1),
                NotificationInterval = TimeSpan.FromSeconds(1),
                NodeExceptionInterval = TimeSpan.FromSeconds(1),
                MonsterCollectionStateInterval = TimeSpan.FromSeconds(1),
                MonsterCollectionStatusInterval = TimeSpan.FromSeconds(1),
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
                ncService.BlockChain,
                "",
                0,
                new RpcContext(),
                new StateMemoryCache()
            );
            services.AddSingleton(publisher);
            services.AddSingleton(StandaloneContextFx);
            services.AddTransient(provider => provider.GetService<StandaloneContext>().BlockChain);
            services.AddSingleton<IWorldStateRepository>(WorldStateRepository.Object);
            services.AddSingleton<IBlockChainRepository>(BlockChainRepository.Object);
            services.AddSingleton<IStateTrieRepository>(StateTrieRepository.Object);
            services.AddSingleton<ITransactionRepository>(TransactionRepository.Object);
            services.AddSingleton<IKeyStore>(KeyStore);
            services.AddSingleton<IConfiguration>(configuration);
            services.AddGraphTypes();
            services.AddLibplanetExplorer();
            services.AddSingleton(ncService);
            services.AddSingleton(ncService.Store);
            services.AddSingleton<StateMemoryCache>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            Schema = new StandaloneSchema(serviceProvider);

            DocumentExecutor = new DocumentExecuter();
        }

        protected Mock<IWorldStateRepository> WorldStateRepository { get; } = new();
        protected Mock<IStateTrieRepository> StateTrieRepository { get; } = new();
        protected Mock<IBlockChainRepository> BlockChainRepository { get; } = new();
        protected Mock<ITransactionRepository> TransactionRepository { get; } = new();
        protected IKeyStore KeyStore { get; } = new InMemoryKeyStore();

        protected PrivateKey AdminPrivateKey { get; } = new PrivateKey();

        protected Address AdminAddress => AdminPrivateKey.Address;

        protected PrivateKey ProposerPrivateKey { get; } = new PrivateKey();

        protected List<PrivateKey> GenesisValidators
        {
            get => new List<PrivateKey>
            {
                ProposerPrivateKey
            }.OrderBy(key => key.Address).ToList();
        }

        protected StandaloneSchema Schema { get; }

        protected StandaloneContext StandaloneContextFx { get; }

        protected BlockChain BlockChain =>
            StandaloneContextFx.BlockChain!;

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
        protected async Task<Task> StartAsync(
            Swarm swarm,
            CancellationToken cancellationToken = default)
        {
            Task task = swarm.StartAsync(
                dialTimeout: TimeSpan.FromMilliseconds(200),
                broadcastBlockInterval: TimeSpan.FromMilliseconds(200),
                broadcastTxInterval: TimeSpan.FromMilliseconds(200),
                cancellationToken: cancellationToken
            );
            await swarm.WaitForRunningAsync();
            return task;
        }

        protected void SetupStatesOnTip(Func<IWorld, IWorld> func)
        {
            var worldState = func(new World(MockUtil.MockModernWorldState));
            var stateRootHash = worldState.Trie.Hash;
            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );
            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);
        }

        protected LibplanetNodeService CreateLibplanetNodeService(
            Block genesisBlock,
            AppProtocolVersion appProtocolVersion,
            PublicKey appProtocolVersionSigner,
            Progress<BlockSyncState>? preloadProgress = null,
            IEnumerable<BoundPeer>? peers = null,
            ImmutableList<BoundPeer>? consensusSeeds = null,
            ImmutableList<BoundPeer>? consensusPeers = null)
        {
            var consensusPrivateKey = new PrivateKey();

            var properties = new LibplanetNodeServiceProperties
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = appProtocolVersion,
                GenesisBlock = genesisBlock,
                StoreStatesCacheSize = 2,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                SwarmPrivateKey = new PrivateKey(),
                ConsensusPrivateKey = consensusPrivateKey,
                ConsensusPort = null,
                Port = null,
                NoMiner = true,
                Render = false,
                Peers = peers ?? ImmutableHashSet<BoundPeer>.Empty,
                TrustedAppProtocolVersionSigners = ImmutableHashSet<PublicKey>.Empty.Add(appProtocolVersionSigner),
                IceServers = ImmutableList<IceServer>.Empty,
                ConsensusSeeds = consensusSeeds ?? ImmutableList<BoundPeer>.Empty,
                ConsensusPeers = consensusPeers ?? ImmutableList<BoundPeer>.Empty,
            };

            return new LibplanetNodeService(
                properties,
                blockPolicy: new BlockPolicy(),
                stagePolicy: new VolatileStagePolicy(),
                renderers: new[] { new DummyRenderer() },
                preloadProgress: preloadProgress,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { },
                actionLoader: StaticActionLoaderSingleton.Instance
            );
        }

        protected BlockCommit? GenerateBlockCommit(long height, BlockHash hash, List<PrivateKey> validators)
        {
            return height != 0
                ? new BlockCommit(
                    height,
                    0,
                    hash,
                    validators.Select(validator => new VoteMetadata(
                            height,
                            0,
                            hash,
                            DateTimeOffset.UtcNow,
                            validator.PublicKey,
                            10_000_000_000_000_000_000,
                            VoteFlag.PreCommit).Sign(validator)).ToImmutableArray())
                : (BlockCommit?)null;
        }

        public void AppendEmptyBlock(IEnumerable<PrivateKey> validators)
        {
            var block = BlockChain.ProposeBlock(ProposerPrivateKey, BlockChain.GetBlockCommit(BlockChain.Tip.Index));
            var blockCommit = GenerateBlockCommit(block.Index, block.Hash, validators.ToList());
            BlockChain.Append(block, blockCommit);
        }
    }
}
