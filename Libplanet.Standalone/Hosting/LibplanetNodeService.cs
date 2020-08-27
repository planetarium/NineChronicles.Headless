using System;
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
using Nito.AsyncEx;
using Serilog;

namespace Libplanet.Standalone.Hosting
{
    public class LibplanetNodeService<T> : IHostedService, IDisposable
        where T : IAction, new()
    {
        public readonly BaseBlockStatesStore Store;

        public readonly IStateStore StateStore;

        public readonly BlockChain<T> BlockChain;

        public readonly Swarm<T> Swarm;

        public AsyncManualResetEvent BootstrapEnded { get; }

        public AsyncManualResetEvent PreloadEnded { get; }

        private Func<BlockChain<T>, Swarm<T>, PrivateKey, CancellationToken, Task> _minerLoopAction;

        private LibplanetNodeServiceProperties<T> _properties;

        private Progress<PreloadState> _preloadProgress;

        private bool _ignoreBootstrapFailure;

        private CancellationToken _swarmCancellationToken;

        private CancellationTokenSource _miningCancellationTokenSource;

        private bool _stopRequested = false;

        private static readonly TimeSpan PingSeedTimeout = TimeSpan.FromSeconds(5);
        
        private static readonly TimeSpan FindNeighborsTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan BootstrapInterval = TimeSpan.FromMinutes(5);

        public LibplanetNodeService(
            LibplanetNodeServiceProperties<T> properties,
            IBlockPolicy<T> blockPolicy,
            IRenderer<T> renderer,
            Func<BlockChain<T>, Swarm<T>, PrivateKey, CancellationToken, Task> minerLoopAction,
            Progress<PreloadState> preloadProgress,
            bool ignoreBootstrapFailure = false
        )
        {
            _properties = properties;

            var genesisBlock = LoadGenesisBlock(properties);

            var iceServers = _properties.IceServers;

            Store = LoadStore(
                _properties.StorePath,
                _properties.StoreType,
                _properties.StoreStatesCacheSize);

            if (properties.Mpt)
            {
                IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(_properties.StorePath, "states")),
                    stateHashKeyValueStore = new RocksDBKeyValueStore(Path.Combine(_properties.StorePath, "state_hashes"));
                StateStore = new TrieStateStore(stateKeyValueStore, stateHashKeyValueStore);
            }
            else
            {
                StateStore = Store;
            }

            var chainIds = Store.ListChainIds().ToList();
            Log.Debug($"Number of chain ids: {chainIds.Count()}");

            foreach (var chainId in chainIds)
            {
                Log.Debug($"chainId: {chainId}");
            }

            // Since <https://github.com/planetarium/libplanet/pull/963> Libplanet became to unify
            // actions renders and tip change events into one interface: IRenderer<T>.
            // The LibplanetNodeServiceProperties.Render option purposes to turn off only action
            // renders, but Libplanet's above patch made it unable to turn off only action renders
            // and leave tip change events turn on.
            // Passing a thunk implementation that leaves action renders blank and fill with
            // only block renders to renderers in the current API is not equivalent giving
            // render: false to BlockChain<T>() constructor in the old API, because it still
            // evaluates actions in order to calculate states of every step, which leads low
            // performance... and that's the reason why we've made
            // LibplanetNodeServiceProperties.Render option.  So...:
            // TODO: We should adjust Libplanet's IRenderer<T> interface so that we can only listen
            // to tip change events and prevent actions to be evaluated.
            BlockChain = new BlockChain<T>(
                policy: blockPolicy,
                store: Store,
                stateStore: StateStore,
                genesisBlock: genesisBlock,
                renderers: renderer is null ? null : new []{ renderer }
            );
            _minerLoopAction = minerLoopAction;
            Swarm = new Swarm<T>(
                BlockChain,
                _properties.PrivateKey,
                _properties.AppProtocolVersion,
                trustedAppProtocolVersionSigners: _properties.TrustedAppProtocolVersionSigners,
                host: _properties.Host,
                listenPort: _properties.Port,
                iceServers: iceServers,
                workers: 50,
                differentAppProtocolVersionEncountered: _properties.DifferentAppProtocolVersionEncountered
            );

            PreloadEnded = new AsyncManualResetEvent();
            BootstrapEnded = new AsyncManualResetEvent();

            _preloadProgress = preloadProgress;
            _ignoreBootstrapFailure = ignoreBootstrapFailure;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            bool preload = true;
            while (!cancellationToken.IsCancellationRequested && !_stopRequested)
            {
                await await Task.WhenAny(
                    StartSwarm(preload, cancellationToken),
                    CheckSwarm(cancellationToken)
                );
                preload = false;
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }

        // 이 privateKey는 swarm에서 사용하는 privateKey와 다를 수 있습니다.
        public void StartMining(PrivateKey privateKey)
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

            _miningCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_swarmCancellationToken);
            Task.Run(
                () => _minerLoopAction(BlockChain, Swarm, privateKey, _miningCancellationTokenSource.Token),
                _miningCancellationTokenSource.Token);
        }

        public void StopMining()
        {
            _miningCancellationTokenSource?.Cancel();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _stopRequested = true;
            StopMining();
            return Swarm.StopAsync(cancellationToken);
        }

        private BaseBlockStatesStore LoadStore(string path, string type, int statesCacheSize)
        {
            BaseBlockStatesStore store = null;

            if (type == "rocksdb")
            {
                try
                {
                    store = new RocksDBStore.RocksDBStore(path, statesCacheSize: statesCacheSize);
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

            return store ?? new DefaultStore(
                path, flush: false, compress: true, statesCacheSize: statesCacheSize);
        }

        private async Task StartSwarm(bool preload, CancellationToken cancellationToken)
        {
            var peers = _properties.Peers.ToImmutableArray();

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
                    await BootstrapSwarmAsync(1);
                    BootstrapEnded.Set();
                }
                catch (PeerDiscoveryException e)
                {
                    Log.Error(e, "Bootstrap failed: {Exception}", e);

                    if (!_ignoreBootstrapFailure)
                    {
                        throw;
                    }
                }

                if (preload)
                {
                    await Swarm.PreloadAsync(
                        TimeSpan.FromSeconds(5),
                        _preloadProgress,
                        _properties.TrustedStateValidators,
                        cancellationToken: cancellationToken
                    );
                    PreloadEnded.Set();
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
                    await Task.Delay(BootstrapInterval);
                    await BootstrapSwarmAsync(0).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Log.Error(t.Exception, "Periodic bootstrap failed.");
                        }
                    });

                    token.ThrowIfCancellationRequested();
                }
            }

            _swarmCancellationToken = cancellationToken;

            try
            {
                await await Task.WhenAny(
                    Swarm.StartAsync(
                        cancellationToken: cancellationToken,
                        millisecondsBroadcastTxInterval: 15000
                    ),
                    ReconnectToSeedPeers(cancellationToken)
                );
            }
            catch (Exception e)
            {
                Log.Error(e, "Unexpected exception occurred during Swarm.StartAsync(). {e}", e);
            }
        }

        private async Task CheckSwarm(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(BootstrapInterval, cancellationToken);
                if (Swarm.LastMessageTimestamp + BootstrapInterval < DateTimeOffset.UtcNow)
                {
                    Log.Error(
                        "No messages have been received since {lastMessageTimestamp}.",
                        Swarm.LastMessageTimestamp
                    );
                    await Swarm.StopAsync(cancellationToken);
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private Block<T> LoadGenesisBlock(LibplanetNodeServiceProperties<T> properties)
        {
            if (!(properties.GenesisBlock is null))
            {
                return properties.GenesisBlock;
            }
            else if (!string.IsNullOrEmpty(properties.GenesisBlockPath))
            {
                var uri = new Uri(_properties.GenesisBlockPath);
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

        public void Dispose()
        {
            Log.Debug($"Disposing {nameof(LibplanetNodeService<T>)}...");

            Swarm?.Dispose();
            Log.Debug("Swarm disposed.");

            Store?.Dispose();
            Log.Debug("Store disposed.");
        }
    }
}
