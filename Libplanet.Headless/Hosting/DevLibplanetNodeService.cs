using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Net.Protocols;
using Libplanet.Store;
using NineChronicles.RPC.Shared.Exceptions;
using Serilog;

namespace Libplanet.Headless.Hosting
{
    public class DevLibplanetNodeService<T> : LibplanetNodeService<T>
        where T : IAction, new()
    {
        private readonly IStore SubStore;

        private readonly IStateStore SubStateStore;

        public readonly BlockChain<T> SubChain;

        public readonly Swarm<T> SubSwarm;

        private Func<Swarm<T>, Swarm<T>, PrivateKey, CancellationToken, Task> _minerLoopAction;

        private bool _stopRequested = false;

        public DevLibplanetNodeService(
            LibplanetNodeServiceProperties<T> properties,
            IBlockPolicy<T> easyPolicy,
            IBlockPolicy<T> hardPolicy,
            IStagePolicy<T> stagePolicy,
            IEnumerable<IRenderer<T>> renderers,
            Func<Swarm<T>, Swarm<T>, PrivateKey, CancellationToken, Task> minerLoopAction,
            Progress<PreloadState> preloadProgress,
            Action<RPCException, string> exceptionHandlerAction,
            Action<bool> preloadStatusHandlerAction,
            bool ignoreBootstrapFailure = false
        ) : base(properties, easyPolicy, stagePolicy, renderers, null, preloadProgress, exceptionHandlerAction, preloadStatusHandlerAction, ignoreBootstrapFailure)
        {
            if (easyPolicy is null)
            {
                throw new ArgumentNullException(nameof(easyPolicy));
            }

            if (hardPolicy is null)
            {
                throw new ArgumentNullException(nameof(hardPolicy));
            }

            Log.Debug("Initializing node service.");

            var genesisBlock = LoadGenesisBlock(properties, hardPolicy.GetHashAlgorithm);

            var iceServers = properties.IceServers;

            (SubStore, SubStateStore) = LoadStore(
                properties.StorePath is null ? null : Path.Combine(properties.StorePath, "sub"),
                properties.StoreType,
                properties.StoreStatesCacheSize);

            SubChain = new BlockChain<T>(
                policy: hardPolicy,
                stagePolicy : stagePolicy,
                store: SubStore,
                stateStore: SubStateStore,
                genesisBlock: genesisBlock
            );

            _minerLoopAction = minerLoopAction;
            var subSwarmPrivateKey = new PrivateKey();
            Log.Debug(
                "Address of sub-swarm is: {address}",
                subSwarmPrivateKey.ToAddress());
            SubSwarm = new Swarm<T>(
                SubChain,
                subSwarmPrivateKey,
                properties.AppProtocolVersion,
                trustedAppProtocolVersionSigners: properties.TrustedAppProtocolVersionSigners,
                host: "localhost",
                listenPort: properties.Port + 1,
                iceServers: iceServers,
                workers: properties.Workers
            );
        }

        public override void StartMining(PrivateKey privateKey)
        {
            if (BlockChain is null)
            {
                throw new InvalidOperationException(
                    $"An exception occurred during {nameof(StartMining)}(). " +
                    $"{nameof(Swarm)} is null.");
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
                () => _minerLoopAction(Swarm, SubSwarm, privateKey, MiningCancellationTokenSource.Token),
                MiningCancellationTokenSource.Token);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _stopRequested = true;
            StopMining();
            return Task.WhenAll(SubSwarm.StopAsync(cancellationToken), Swarm.StopAsync(cancellationToken));
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
            => Task.Run(async () =>
            {
                bool preload = true;
                while (!cancellationToken.IsCancellationRequested && !_stopRequested)
                {
                    var tasks = new List<Task>
                    {
                        StartSwarm(preload, cancellationToken),
                        CheckMessage(Properties.MessageTimeout, cancellationToken),
                    };
                    if (Properties.Peers.Any())
                    {
                        tasks.Add(CheckPeerTable(cancellationToken));
                    }
                    await await Task.WhenAny(tasks);
                    preload = false;
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
            });

        private async Task StartSwarm(bool preload, CancellationToken cancellationToken)
        {
            Log.Debug("Starting swarms.");
            var peers = Properties.Peers.ToImmutableArray();

            Task BootstrapMainSwarmAsync(int depth)
                => Swarm.BootstrapAsync(
                    peers,
                    depth: depth,
                    cancellationToken: cancellationToken
                );

            if (peers.Any())
            {
                try
                {
                    // FIXME: It's safe to increase depth.
                    await BootstrapMainSwarmAsync(1);
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
                    await Swarm.PreloadAsync(
                        PreloadProgress,
                        cancellationToken: cancellationToken
                    );
                    PreloadEnded.Set();
                }
            }

            async Task ReconnectToSeedPeers(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(BootstrapInterval);
                    await BootstrapMainSwarmAsync(0).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Log.Error(t.Exception, "Periodic bootstrap failed.");
                        }
                    });

                    token.ThrowIfCancellationRequested();
                }
            }

            SwarmCancellationToken = cancellationToken;

            try
            {
                var t = Swarm.StartAsync(cancellationToken);
                await Swarm.WaitForRunningAsync();
                await SubSwarm.BootstrapAsync(
                    new []{ Swarm.AsPeer },
                    1,
                    cancellationToken);
                await await Task.WhenAny(
                    t,
                    SubSwarm.StartAsync(cancellationToken),
                    ReconnectToSeedPeers(cancellationToken)
                );
            }
            catch (Exception e)
            {
                Log.Error(e, "Unexpected exception occurred during Swarm.StartAsync(). {e}", e);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Log.Debug($"Disposing {nameof(DevLibplanetNodeService<T>)}...");

            SubSwarm?.Dispose();
            Log.Debug("Sub swarm disposed.");

            (SubStore as IDisposable)?.Dispose();
            Log.Debug("Sub store disposed.");
        }
    }
}
