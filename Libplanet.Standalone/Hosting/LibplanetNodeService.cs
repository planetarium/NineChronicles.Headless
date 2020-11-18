using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Net.Protocols;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Hosting;
using NineChronicles.RPC.Shared.Exceptions;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;

namespace Libplanet.Standalone.Hosting
{
    public class LibplanetNodeService<T> : IHostedService, IDisposable
        where T : IAction, new()
    {
        public readonly BaseBlockStatesStore Store;

        public readonly IStateStore StateStore;

        public readonly BlockChain<T> BlockChain;

        public readonly Swarm<T> Swarm;

        public readonly LibplanetNodeServiceProperties<T> Properties;
        
        public AsyncManualResetEvent BootstrapEnded { get; }

        public AsyncManualResetEvent PreloadEnded { get; }

        private Func<BlockChain<T>, Swarm<T>, PrivateKey, CancellationToken, Task> _minerLoopAction;

        private Action<RPCException, string> _exceptionHandlerAction;

        private Action<bool> _preloadStatusHandlerAction;

        protected Progress<PreloadState> PreloadProgress;

        protected bool IgnoreBootstrapFailure;

        protected CancellationToken SwarmCancellationToken;

        protected CancellationTokenSource MiningCancellationTokenSource;

        private bool _stopRequested = false;

        protected static readonly TimeSpan PingSeedTimeout = TimeSpan.FromSeconds(5);

        protected static readonly TimeSpan FindNeighborsTimeout = TimeSpan.FromSeconds(5);

        protected static readonly TimeSpan BootstrapInterval = TimeSpan.FromMinutes(5);

        protected static readonly TimeSpan CheckPeerTableInterval = TimeSpan.FromSeconds(10);

        public LibplanetNodeService(
            LibplanetNodeServiceProperties<T> properties,
            IBlockPolicy<T> blockPolicy,
            IEnumerable<IRenderer<T>> renderers,
            Func<BlockChain<T>, Swarm<T>, PrivateKey, CancellationToken, Task> minerLoopAction,
            Progress<PreloadState> preloadProgress,
            Action<RPCException, string> exceptionHandlerAction,
            Action<bool> preloadStatusHandlerAction, 
            bool ignoreBootstrapFailure = false
        )
        {
            if (blockPolicy is null)
            {
                throw new ArgumentNullException(nameof(blockPolicy));
            }

            Properties = properties;

            var genesisBlock = LoadGenesisBlock(properties);

            var iceServers = Properties.IceServers;

            (Store, StateStore) = LoadStore(
                Properties.StorePath,
                Properties.StoreType,
                Properties.StoreStatesCacheSize,
                Properties.Mpt);

            var pendingTxs = Store.IterateStagedTransactionIds()
                .ToImmutableHashSet();
            Store.UnstageTransactionIds(pendingTxs);
            Log.Debug("Pending txs unstaged. [{PendingCount}]", pendingTxs.Count);

            var chainIds = Store.ListChainIds().ToList();
            Log.Debug($"Number of chain ids: {chainIds.Count()}");

            foreach (var chainId in chainIds)
            {
                Log.Debug($"chainId: {chainId}");
            }

            if (Properties.Confirmations > 0)
            {
                renderers = renderers.Select(r => r is IActionRenderer<T> ar
                    ? new DelayedActionRenderer<T>(ar, Store, Properties.Confirmations)
                    : new DelayedRenderer<T>(r, Store, Properties.Confirmations)
                );

                // Log the outmost (before delayed) events as well as
                // the innermost (after delayed) events:
                ILogger logger = Log.ForContext("SubLevel", " RAW-RENDER-EVENT");
                renderers = renderers.Select(r => r is IActionRenderer<T> ar
                    ? new LoggedActionRenderer<T>(ar, logger, LogEventLevel.Debug)
                    : new LoggedRenderer<T>(r, logger, LogEventLevel.Debug)
                );
            }

            BlockChain = new BlockChain<T>(
                policy: blockPolicy,
                store: Store,
                stateStore: StateStore,
                genesisBlock: genesisBlock,
                renderers: renderers
            );

            foreach (Guid chainId in chainIds.Where(chainId => chainId != BlockChain.Id))
            {
                Store.DeleteChainId(chainId);
            }

            _minerLoopAction = minerLoopAction;
            _exceptionHandlerAction = exceptionHandlerAction;
            _preloadStatusHandlerAction = preloadStatusHandlerAction;
            IEnumerable<IceServer> shuffledIceServers = null;
            if (!(iceServers is null))
            {
                var rand = new Random();
                shuffledIceServers = iceServers.OrderBy(x => rand.Next());
            }

            Swarm = new Swarm<T>(
                BlockChain,
                Properties.PrivateKey,
                Properties.AppProtocolVersion,
                trustedAppProtocolVersionSigners: Properties.TrustedAppProtocolVersionSigners,
                host: Properties.Host,
                listenPort: Properties.Port,
                iceServers: shuffledIceServers,
                workers: Properties.Workers,
                differentAppProtocolVersionEncountered: Properties.DifferentAppProtocolVersionEncountered,
                options: new SwarmOptions
                {
                    MaxTimeout = TimeSpan.FromSeconds(10),
                    BlockHashRecvTimeout = TimeSpan.FromSeconds(10),
                }
            );

            PreloadEnded = new AsyncManualResetEvent();
            BootstrapEnded = new AsyncManualResetEvent();

            PreloadProgress = preloadProgress;
            IgnoreBootstrapFailure = ignoreBootstrapFailure;
        }

        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            bool preload = true;
            while (!cancellationToken.IsCancellationRequested && !_stopRequested)
            {
                var tasks = new List<Task>
                {
                    StartSwarm(preload, cancellationToken),
                    CheckMessage(cancellationToken),
                    CheckTip(cancellationToken)
                };
                if (Properties.Peers.Any())
                {
                    tasks.Add(CheckDemand(cancellationToken));
                    tasks.Add(CheckPeerTable(cancellationToken));
                }
                await await Task.WhenAny(tasks);
                preload = false;
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }

        // 이 privateKey는 swarm에서 사용하는 privateKey와 다를 수 있습니다.
        public virtual void StartMining(PrivateKey privateKey)
        {
            if (BlockChain is null)
            {
                throw new InvalidOperationException(
                    $"An exception occurred during {nameof(StartMining)}(). " +
                    $"{nameof(BlockChain)} is null.");
            }

            if (Swarm is null)
            {
                throw new InvalidOperationException(
                    $"An exception occurred during {nameof(StartMining)}(). " +
                    $"{nameof(Swarm)} is null.");
            }

            MiningCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(SwarmCancellationToken);
            Task.Run(
                () => _minerLoopAction(BlockChain, Swarm, privateKey, MiningCancellationTokenSource.Token),
                MiningCancellationTokenSource.Token);
        }

        public void StopMining()
        {
            MiningCancellationTokenSource?.Cancel();
        }

        public async Task<bool> CheckPeer(string addr)
        {
            var address = new Address(addr);
            var boundPeer = await Swarm.FindSpecificPeerAsync(
                address, -1, cancellationToken: SwarmCancellationToken);
            return !(boundPeer is null);
        }

        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            _stopRequested = true;
            StopMining();
            return Swarm.StopAsync(cancellationToken);
        }

        protected (BaseBlockStatesStore, IStateStore) LoadStore(string path, string type, int statesCacheSize, bool mpt)
        {
            BaseBlockStatesStore store = null;
            IStateStore stateStore = null;

            if (type == "rocksdb")
            {
                try
                {
                    store = new RocksDBStore.RocksDBStore(
                        path,
                        statesCacheSize: statesCacheSize,
                        maxTotalWalSize: 16 * 1024 * 1024,
                        keepLogFileNum: 1);
                    Log.Debug("RocksDB is initialized.");
                }
                catch (TypeInitializationException e)
                {
                    Log.Error("RocksDB is not available. DefaultStore will be used. {0}", e);
                }
            }
            else
            {
                var message = type is null
                    ? "Storage Type is not specified"
                    : $"Storage Type {type} is not supported";
                Log.Debug($"{message}. DefaultStore will be used.");
            }

            store ??= new DefaultStore(
                path, flush: false, compress: true, statesCacheSize: statesCacheSize);

            if (mpt)
            {
                IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(path, "states")),
                    stateHashKeyValueStore = new RocksDBKeyValueStore(Path.Combine(path, "state_hashes"));
                stateStore = new TrieStateStore(stateKeyValueStore, stateHashKeyValueStore);
            }
            else
            {
                stateStore = store;
            }

            return (store, stateStore);
        }

        private async Task StartSwarm(bool preload, CancellationToken cancellationToken)
        {
            var peers = Properties.Peers.ToImmutableArray();

            Task BootstrapSwarmAsync(int depth)
                => Swarm.BootstrapAsync(
                    peers,
                    pingSeedTimeout: PingSeedTimeout,
                    findNeighborsTimeout: FindNeighborsTimeout,
                    depth: depth,
                    cancellationToken: cancellationToken
                );

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
                    await Swarm.PreloadAsync(
                        TimeSpan.FromSeconds(5),
                        PreloadProgress,
                        Properties.TrustedStateValidators,
                        cancellationToken: cancellationToken
                    );
                    PreloadEnded.Set();
                    _preloadStatusHandlerAction(false);
                }
            }
            else if (preload)
            {
                BootstrapEnded.Set();
                PreloadEnded.Set();
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
                        Swarm.StartAsync(
                            cancellationToken: cancellationToken,
                            millisecondsBroadcastTxInterval: 15000
                        ),
                        ReconnectToSeedPeers(cancellationToken)
                    );
                }
                else
                {
                    await Swarm.StartAsync(
                        cancellationToken: cancellationToken,
                        millisecondsBroadcastTxInterval: 15000);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Unexpected exception occurred during Swarm.StartAsync(). {e}", e);
            }
        }

        protected async Task CheckMessage(CancellationToken cancellationToken = default)
        {
            var messageTimeout = TimeSpan.FromMinutes(1);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(BootstrapInterval, cancellationToken);
                if (Swarm.LastMessageTimestamp + messageTimeout < DateTimeOffset.UtcNow)
                {
                    var message =
                        $"No messages have been received since {Swarm.LastMessageTimestamp}.";
                        
                    Log.Error(message);
                    Properties.NodeExceptionOccurred(NodeExceptionType.MessageNotReceived, message);
                    await Swarm.StopAsync(cancellationToken);
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // FIXME: Can fixed by just restarting Swarm only (i.e. CheckMessage)
        private async Task CheckTip(CancellationToken cancellationToken = default)
        {
            var tipTimeout = TimeSpan.FromMinutes(2);
            var lastTipChanged = DateTimeOffset.Now;
            var lastTip = BlockChain.Tip;
            while (!cancellationToken.IsCancellationRequested)
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
                        $"Chain's tip is stale. (index: {BlockChain.Tip?.Index}, " +
                        $"hash: {BlockChain.Tip?.Hash}, timeout: {tipTimeout})";
                    Log.Error(message);
                    Properties.NodeExceptionOccurred(NodeExceptionType.TipNotChange, message);
                    _stopRequested = true;
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async Task CheckDemand(CancellationToken cancellationToken = default)
        {
            const int buffer = 1150;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                if (!Swarm.Running)
                {
                    continue;
                }
                
                if ((Swarm.BlockDemand?.Header.Index ?? 0) > (BlockChain.Tip?.Index ?? 0) + buffer)
                {
                    var message =
                        $"Chain's tip is too low. (demand: {Swarm.BlockDemand?.Header.Index}, " +
                        $"actual: {BlockChain.Tip?.Index}, buffer: {buffer})";
                    Log.Error(message);
                    Properties.NodeExceptionOccurred(NodeExceptionType.DemandTooHigh, message);
                    _stopRequested = true;
                    break;
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

        protected Block<T> LoadGenesisBlock(LibplanetNodeServiceProperties<T> properties)
        {
            if (!(properties.GenesisBlock is null))
            {
                return properties.GenesisBlock;
            }
            else if (!string.IsNullOrEmpty(properties.GenesisBlockPath))
            {
                var uri = new Uri(properties.GenesisBlockPath);
                using var client = new WebClient();
                var rawGenesisBlock = client.DownloadData(uri);
                return Block<T>.Deserialize(rawGenesisBlock);
            }
            else
            {
                throw new ArgumentException(
                    $"At least, one of {nameof(LibplanetNodeServiceProperties<T>.GenesisBlock)} or {nameof(LibplanetNodeServiceProperties<T>.GenesisBlockPath)} must be set.");
            }
        }

        public virtual void Dispose()
        {
            Log.Debug($"Disposing {nameof(LibplanetNodeService<T>)}...");

            Swarm?.Dispose();
            Log.Debug("Swarm disposed.");

            Store?.Dispose();
            Log.Debug("Store disposed.");
        }
    }
}
