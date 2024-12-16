using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Types.Blocks;
using Libplanet.Crypto;
using Libplanet.Extensions.ForkableActionEvaluator;
using Libplanet.Extensions.PluggedActionEvaluator;
using Libplanet.Net;
using Libplanet.Net.Consensus;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Hosting;
using NineChronicles.RPC.Shared.Exceptions;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;

namespace Libplanet.Headless.Hosting
{
    public class LibplanetNodeService : BackgroundService, IDisposable
    {
        private static readonly Codec Codec = new Codec();

        public readonly IStore Store;

        public readonly IStateStore StateStore;

        public readonly IKeyValueStore StateKeyValueStore;

        public readonly BlockChain BlockChain;

        public readonly Swarm Swarm;

        public readonly LibplanetNodeServiceProperties Properties;

        public AsyncManualResetEvent BootstrapEnded { get; }

        public AsyncManualResetEvent PreloadEnded { get; }

        private readonly bool _ignorePreloadFailure;

        private Action<RPCException, string> _exceptionHandlerAction;

        private Action<bool> _preloadStatusHandlerAction;

        protected Progress<BlockSyncState> PreloadProgress;

        protected bool IgnoreBootstrapFailure;

        protected CancellationToken SwarmCancellationToken;

        private bool _stopRequested = false;

        protected static readonly TimeSpan BootstrapInterval = TimeSpan.FromMinutes(5);

        protected static readonly TimeSpan CheckPeerTableInterval = TimeSpan.FromSeconds(10);

        private List<Guid> _obsoletedChainIds;

        public LibplanetNodeService(
            LibplanetNodeServiceProperties properties,
            IBlockPolicy blockPolicy,
            IStagePolicy stagePolicy,
            IEnumerable<IRenderer> renderers,
            Progress<BlockSyncState> preloadProgress,
            Action<RPCException, string> exceptionHandlerAction,
            Action<bool> preloadStatusHandlerAction,
            IActionLoader actionLoader,
            bool ignoreBootstrapFailure = false,
            bool ignorePreloadFailure = false
        )
        {
            if (blockPolicy is null)
            {
                throw new ArgumentNullException(nameof(blockPolicy));
            }

            Properties = properties;

            var genesisBlock = LoadGenesisBlock(properties);

            var iceServers = Properties.IceServers;

            (Store, StateStore, IKeyValueStore keyValueStore) = LoadStore(
                Properties.StorePath,
                Properties.StoreType,
                Properties.StoreStatesCacheSize);

            var chainIds = Store.ListChainIds().ToList();
            Log.Debug($"Number of chain ids: {chainIds.Count()}");
            Log.Debug($"Canonical chain id: {Store.GetCanonicalChainId().ToString()}");

            if (Properties.Confirmations > 0)
            {
                // Log the outmost (before delayed) events as well as
                // the innermost (after delayed) events:
                ILogger logger = Log.ForContext("SubLevel", " RAW-RENDER-EVENT");
                renderers = renderers.Select(r => r is IActionRenderer ar
                    ? new LoggedActionRenderer(ar, logger, LogEventLevel.Debug)
                    : new LoggedRenderer(r, logger, LogEventLevel.Debug)
                );
            }

            var blockChainStates = new BlockChainStates(Store, StateStore);

            IActionEvaluator BuildActionEvaluator(IActionEvaluatorConfiguration actionEvaluatorConfiguration)
            {
                return actionEvaluatorConfiguration switch
                {
                    PluggedActionEvaluatorConfiguration pluginActionEvaluatorConfiguration =>
                        new PluggedActionEvaluator(
                            ResolvePluginPath(pluginActionEvaluatorConfiguration.PluginPath),
                            pluginActionEvaluatorConfiguration.TypeName,
                            keyValueStore,
                            actionLoader),
                    DefaultActionEvaluatorConfiguration _ =>
                        new ActionEvaluator(
                            policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                            stateStore: StateStore,
                            actionTypeLoader: actionLoader),
                    ForkableActionEvaluatorConfiguration forkableActionEvaluatorConfiguration =>
                        new ForkableActionEvaluator(
                            forkableActionEvaluatorConfiguration.Pairs.Select(
                                pair => (
                                        (pair.Item1.Start, pair.Item1.End),
                                        BuildActionEvaluator(pair.Item2))),
                            actionLoader),
                    _ => throw new InvalidOperationException("Unexpected type."),
                };
            }

            IActionEvaluator actionEvaluator = BuildActionEvaluator(properties.ActionEvaluatorConfiguration);

            if (Store.GetCanonicalChainId() is { })
            {
                BlockChain = new BlockChain(
                    policy: blockPolicy,
                    store: Store,
                    stagePolicy: stagePolicy,
                    stateStore: StateStore,
                    genesisBlock: genesisBlock,
                    renderers: renderers,
                    blockChainStates: blockChainStates,
                    actionEvaluator: actionEvaluator
                );
            }
            else
            {
                BlockChain = BlockChain.Create(
                    policy: blockPolicy,
                    store: Store,
                    stagePolicy: stagePolicy,
                    stateStore: StateStore,
                    genesisBlock: genesisBlock,
                    renderers: renderers,
                    blockChainStates: blockChainStates,
                    actionEvaluator: actionEvaluator
                );
            }

            StateKeyValueStore = keyValueStore;

            _obsoletedChainIds = chainIds.Where(chainId => chainId != BlockChain.Id).ToList();

            _exceptionHandlerAction = exceptionHandlerAction;
            _preloadStatusHandlerAction = preloadStatusHandlerAction;
            IEnumerable<IceServer> shuffledIceServers = null;
            if (!(iceServers is null))
            {
                var rand = new Random();
                shuffledIceServers = iceServers.OrderBy(x => rand.Next());
            }

            var appProtocolVersionOptions = new AppProtocolVersionOptions
            {
                AppProtocolVersion = Properties.AppProtocolVersion,
                TrustedAppProtocolVersionSigners =
                    Properties.TrustedAppProtocolVersionSigners?.ToImmutableHashSet() ?? ImmutableHashSet<PublicKey>.Empty,
                DifferentAppProtocolVersionEncountered = Properties.DifferentAppProtocolVersionEncountered,
            };
            var hostOptions = new Net.Options.HostOptions(Properties.Host, shuffledIceServers, Properties.Port ?? default);
            var swarmOptions = new Net.Options.SwarmOptions
            {
                MinimumBroadcastTarget = Properties.MinimumBroadcastTarget,
                BucketSize = Properties.BucketSize,
                MaximumPollPeers = Properties.MaximumPollPeers,
                TimeoutOptions = new Net.Options.TimeoutOptions
                {
                    MaxTimeout = TimeSpan.FromSeconds(50),
                    GetBlockHashesTimeout = TimeSpan.FromSeconds(50),
                    GetBlocksBaseTimeout = TimeSpan.FromSeconds(5),
                },
            };

            var transport = NetMQTransport.Create(
                Properties.SwarmPrivateKey,
                appProtocolVersionOptions,
                hostOptions,
                swarmOptions.MessageTimestampBuffer).ConfigureAwait(false).GetAwaiter().GetResult();

            NetMQTransport consensusTransport = null;
            ConsensusReactorOption? consensusReactorOption = null;

            // One of consensus seeds or consensus peers should not be null
            if (!(Properties.ConsensusPrivateKey is null) &&
                !(Properties.Host is null) &&
                !(Properties.ConsensusPort is null) &&
                !(Properties.ConsensusSeeds is null && Properties.ConsensusPeers is null))
            {
                consensusTransport = NetMQTransport.Create(
                    Properties.ConsensusPrivateKey,
                    appProtocolVersionOptions,
                    new Net.Options.HostOptions(Properties.Host, shuffledIceServers, (int)Properties.ConsensusPort),
                    swarmOptions.MessageTimestampBuffer).ConfigureAwait(false).GetAwaiter().GetResult();
                consensusReactorOption = new ConsensusReactorOption
                {
                    SeedPeers = Properties.ConsensusSeeds!,
                    ConsensusPeers = Properties.ConsensusPeers!,
                    ConsensusPort = (int)Properties.ConsensusPort,
                    ConsensusPrivateKey = Properties.ConsensusPrivateKey,
                    ConsensusWorkers = 500,
                    TargetBlockInterval = TimeSpan.FromMilliseconds(Properties.ConsensusTargetBlockIntervalMilliseconds ?? 7000),
                    ContextOption = Properties.ContextOption,
                };
            }

            Log.Debug(
                "Initializing {Swarm}. {Reactor}: {Validator}",
                nameof(Swarm),
                nameof(ConsensusReactor),
                !(consensusReactorOption is null));

            Swarm = new Swarm(
                BlockChain,
                Properties.SwarmPrivateKey,
                transport,
                swarmOptions,
                consensusTransport,
                consensusOption: consensusReactorOption);

            PreloadEnded = new AsyncManualResetEvent();
            BootstrapEnded = new AsyncManualResetEvent();

            PreloadProgress = preloadProgress;
            IgnoreBootstrapFailure = ignoreBootstrapFailure;
            _ignorePreloadFailure = ignorePreloadFailure;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
            => Task.Run(async () =>
            {
                Log.Debug("Trying to delete {count} obsoleted chains...", _obsoletedChainIds.Count());
                _ = Task.Run(() =>
                {
                    foreach (Guid chainId in _obsoletedChainIds)
                    {
                        Store.DeleteChainId(chainId);
                        Log.Debug("Obsoleted chain[{chainId}] has been deleted.", chainId);
                    }
                });
                if (!cancellationToken.IsCancellationRequested && !_stopRequested)
                {
                    var tasks = new List<Task>
                    {
                        StartSwarm(Properties.Preload, cancellationToken),
                        CheckMessage(Properties.MessageTimeout, cancellationToken),
                        CheckTip(Properties.TipTimeout, cancellationToken)
                    };
                    if (Properties.Peers.Any())
                    {
                        tasks.Add(CheckPeerTable(cancellationToken));
                    }

                    await await Task.WhenAny(tasks);
                }
            });

        public async Task<bool> CheckPeer(string addr)
        {
            var address = new Address(addr);
            var boundPeer = await Swarm.FindSpecificPeerAsync(
                address, -1, cancellationToken: SwarmCancellationToken);
            return !(boundPeer is null);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopRequested = true;
            await Swarm.StopAsync(cancellationToken);
            foreach (IRenderer renderer in BlockChain.Renderers)
            {
                if (renderer is IDisposable disposableRenderer)
                {
                    disposableRenderer.Dispose();
                }
            }
        }

        protected (IStore, IStateStore, IKeyValueStore) LoadStore(string path, string type, int statesCacheSize)
        {
            IStore store = null;
            if (type == "rocksdb")
            {
                try
                {
                    Log.Debug("Migrating RocksDB.");
                    var chainPath = Path.Combine(path, "chain");
                    if (Directory.Exists(chainPath) &&
                        RocksDBStore.RocksDBStore.MigrateChainDBFromColumnFamilies(chainPath))
                    {
                        Log.Debug("RocksDB is migrated.");
                    }

                    store = new RocksDBStore.RocksDBStore(
                        path,
                        maxTotalWalSize: 16 * 1024 * 1024,
                        maxLogFileSize: 16 * 1024 * 1024,
                        keepLogFileNum: 1
                    );
                    Log.Debug("RocksDB is initialized.");
                }
                catch (TypeInitializationException e)
                {
                    Log.Error("RocksDB is not available. DefaultStore will be used. {0}", e);
                }
            }
            else if (type == "memory")
            {
                store = new MemoryStore();
            }
            else
            {
                var message = type is null
                    ? "Storage Type is not specified"
                    : $"Storage Type {type} is not supported";
                Log.Debug($"{message}. DefaultStore will be used.");
            }

            store ??= new DefaultStore(path, flush: false);

            IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(path, "states"));
            IStateStore stateStore = new TrieStateStore(stateKeyValueStore);
            return (store, stateStore, stateKeyValueStore);
        }

        private async Task StartSwarm(bool preload, CancellationToken cancellationToken)
        {
            var peers = Properties.Peers.ToImmutableArray();

            Task BootstrapSwarmAsync(int depth)
                => Swarm.BootstrapAsync(
                    seedPeers: peers,
                    searchDepth: depth,
                    dialTimeout: null,
                    cancellationToken: cancellationToken);

            if (peers.Any())
            {
                try
                {
                    // FIXME: It's safe to increase depth.
                    await BootstrapSwarmAsync(1);
                    BootstrapEnded.Set();
                }
                catch (PeerDiscoveryException e)
                {
                    Log.Error(e, "Bootstrap failed: {Exception}", e);

                    if (!IgnoreBootstrapFailure)
                    {
                        throw;
                    }
                }

                if (preload)
                {
                    _preloadStatusHandlerAction(true);
                    try
                    {
                        await Swarm.PreloadAsync(
                            PreloadProgress,
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (AggregateException e)
                    {
                        Log.Error(e, "{Message}", e.Message);
                        if (!_ignorePreloadFailure)
                        {
                            throw;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(
                            e,
                            $"An unexpected exception occurred during {nameof(Swarm.PreloadAsync)}: {{Message}}",
                            e.Message
                        );
                        if (!_ignorePreloadFailure)
                        {
                            throw;
                        }
                    }

                    PreloadEnded.Set();
                    _preloadStatusHandlerAction(false);
                }
            }
            else if (preload)
            {
                _preloadStatusHandlerAction(true);
                BootstrapEnded.Set();
                PreloadEnded.Set();
                _preloadStatusHandlerAction(false);
            }

            async Task ReconnectToSeedPeers(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(BootstrapInterval, token);
                    await BootstrapSwarmAsync(0).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Log.Information(t.Exception, "Periodic bootstrap failed.");
                        }
                    }, token);

                    token.ThrowIfCancellationRequested();
                }
            }

            SwarmCancellationToken = cancellationToken;

            try
            {
                if (peers.Any())
                {
                    await await Task.WhenAny(
                        Swarm.StartAsync(cancellationToken),
                        ReconnectToSeedPeers(cancellationToken)
                    );
                }
                else
                {
                    await Swarm.StartAsync(cancellationToken);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Unexpected exception occurred during Swarm.StartAsync(). {e}", e);
            }
        }

        protected async Task CheckMessage(TimeSpan messageTimeout, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(BootstrapInterval, cancellationToken);
                if (Swarm.LastMessageTimestamp + messageTimeout < DateTimeOffset.UtcNow)
                {
                    var message =
                        $"No messages have been received since {Swarm.LastMessageTimestamp}.";

                    Log.Error(message);
                    Properties.NodeExceptionOccurred(NodeExceptionType.MessageNotReceived, message);
                    _stopRequested = true;
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // FIXME: Can fixed by just restarting Swarm only (i.e. CheckMessage)
        private async Task CheckTip(TimeSpan tipTimeout, CancellationToken cancellationToken = default)
        {
            var lastTipChanged = DateTimeOffset.Now;
            var lastTip = BlockChain.Tip;
            bool exit = false;
            while (!cancellationToken.IsCancellationRequested && !exit)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                if (!Swarm.Running)
                {
                    continue;
                }

                if (lastTip != BlockChain.Tip)
                {
                    lastTip = BlockChain.Tip;
                    lastTipChanged = DateTimeOffset.Now;
                }

                if (lastTipChanged + tipTimeout < DateTimeOffset.Now)
                {
                    var message =
                        $"Chain's tip is stale. (index: {BlockChain.Tip.Index}, " +
                        $"hash: {BlockChain.Tip.Hash}, timeout: {tipTimeout})";
                    Log.Error(message);

                    // TODO: Use flag to determine behavior when the chain's tip is stale.
                    switch (Properties.ChainTipStaleBehavior)
                    {
                        case "reboot":
                            Properties.NodeExceptionOccurred(
                                NodeExceptionType.TipNotChange,
                                message);
                            _stopRequested = true;
                            exit = true;
                            break;

                        case "preload":
                            try
                            {
                                Log.Error("Start preloading due to staled tip.");
                                await Swarm.PreloadAsync(
                                    PreloadProgress,
                                    cancellationToken: cancellationToken
                                );
                                Log.Error(
                                    "Preloading successfully finished. " +
                                    "(index: {Index}, hash: {Hash})",
                                    BlockChain.Tip.Index,
                                    BlockChain.Tip.Hash);
                            }
                            catch (Exception e)
                            {
                                Log.Error(
                                    e,
                                    $"An unexpected exception occurred during " +
                                    $"{nameof(Swarm.PreloadAsync)}: {{Message}}",
                                    e.Message
                                );
                            }
                            break;

                        default:
                            throw new ArgumentException(nameof(Properties.ChainTipStaleBehavior));
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // FIXME: Can fixed by just restarting Swarm only (i.e. CheckMessage)
        protected async Task CheckPeerTable(CancellationToken cancellationToken = default)
        {
            const int grace = 3;
            var count = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(CheckPeerTableInterval, cancellationToken);
                if (!Swarm.Peers.Any())
                {
                    if (grace == count)
                    {
                        var message = "No any peers are connected even seed peers were given. " +
                            $"(grace: {grace}";
                        Log.Error(message);
                        // _exceptionHandlerAction(RPCException.NetworkException, message);
                        Properties.NodeExceptionOccurred(NodeExceptionType.NoAnyPeer, message);
                        _stopRequested = true;
                        break;
                    }

                    count++;
                }
                else
                {
                    count = 0;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        protected Block LoadGenesisBlock(
            LibplanetNodeServiceProperties properties
        )
        {
            if (!(properties.GenesisBlock is null))
            {
                return properties.GenesisBlock;
            }
            else if (!string.IsNullOrEmpty(properties.GenesisBlockPath))
            {
                byte[] rawBlock;
                if (File.Exists(Path.GetFullPath(properties.GenesisBlockPath)))
                {
                    rawBlock = File.ReadAllBytes(Path.GetFullPath(properties.GenesisBlockPath));
                }
                else
                {
                    var uri = new Uri(properties.GenesisBlockPath);
                    using var client = new HttpClient();
                    rawBlock = client.GetByteArrayAsync(uri).Result;
                }
                var blockDict = (Bencodex.Types.Dictionary)Codec.Decode(rawBlock);
                return BlockMarshaler.UnmarshalBlock(blockDict);
            }
            else
            {
                throw new ArgumentException(
                    $"At least, one of {nameof(LibplanetNodeServiceProperties.GenesisBlock)} or {nameof(LibplanetNodeServiceProperties.GenesisBlockPath)} must be set.");
            }
        }

        public override void Dispose()
        {
            Log.Debug($"Disposing {nameof(LibplanetNodeService)}...");

            Swarm?.Dispose();
            Log.Debug("Swarm disposed.");

            (Store as IDisposable)?.Dispose();
            Log.Debug("Store disposed.");
        }

        private string ResolvePluginPath(string path)
        {
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
            {
                // Run the download on a background thread
                return Task.Run(() => DownloadPlugin(path)).GetAwaiter().GetResult();
            }

            return path;
        }

        private async Task<string> DownloadPlugin(string url)
        {
            var basePath = Environment.GetEnvironmentVariable("PLUGIN_PATH") ?? Environment.CurrentDirectory;
            var path = Path.Combine(basePath, "plugins");
            Directory.CreateDirectory(path);
            var hashed = CreateSha256Hash(url);
            var logger = Log.ForContext("LibplanetNodeService", hashed);
            using var httpClient = new HttpClient();
            var downloadPath = Path.Join(path, hashed + ".zip");
            var extractPath = Path.Join(path, hashed);
            var checksumPath = Path.Join(extractPath, "checksum.txt");

            logger.Debug("Download Path: {downloadPath}", downloadPath);
            logger.Debug("Extract Path: {extractPath}", extractPath);

            if (!File.Exists(downloadPath))
            {
                logger.Debug("Downloading...");
                var content = await httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(downloadPath, content);
                logger.Debug("Finished downloading.");
            }
            else
            {
                logger.Debug("Download skipped, file already exists.");
            }

            if (!Directory.Exists(extractPath) || (File.Exists(checksumPath) && CalculateDirectoryChecksum(extractPath) != await File.ReadAllTextAsync(checksumPath)))
            {
                Directory.CreateDirectory(extractPath);
                logger.Debug("Extracting...");
                ZipFile.ExtractToDirectory(downloadPath, extractPath, true);
                logger.Debug("Finished extracting.");

                // Calculate checksum of extracted files and save it
                var checksum = CalculateDirectoryChecksum(extractPath);
                await File.WriteAllTextAsync(checksumPath, checksum);
            }
            else
            {
                logger.Debug("Extraction skipped, folder already exists and is verified.");
            }

            return Path.Combine(extractPath, "Lib9c.Plugin.dll");
        }

        private string CalculateDirectoryChecksum(string directoryPath)
        {
            using var md5 = MD5.Create();
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                .OrderBy(p => p).ToArray();

            foreach (var file in files)
            {
                // hash path
                var pathBytes = Encoding.UTF8.GetBytes(file.Substring(directoryPath.Length));
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                // hash contents
                var contentBytes = File.ReadAllBytes(file);
                md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            }

            // Handles empty content.
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(md5.Hash!).Replace("-", "").ToLowerInvariant();
        }

        // FIXME: Request libplanet provide default implementation.
        private sealed class ActionTypeLoaderContext : IActionTypeLoaderContext
        {
            public ActionTypeLoaderContext(long index)
            {
                Index = index;
            }

            public long Index { get; }
        }

        private static string CreateSha256Hash(string input)
        {
            using SHA256 sha256Hash = SHA256.Create();

            // ComputeHash - returns byte array
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Convert byte array to a string
            StringBuilder builder = new StringBuilder();
            foreach (var t in bytes)
            {
                builder.Append(t.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
