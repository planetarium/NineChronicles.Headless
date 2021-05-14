using Bencodex.Types;
using Lib9c.Renderer;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Crypto;
using Libplanet.Headless;
using Libplanet.Headless.Hosting;
using Libplanet.Net;
using Libplanet.Store;
using Microsoft.Extensions.Hosting;
using Nekoyume.Action;
using Nekoyume.BlockChain;
using Nekoyume.Model.State;
using NineChronicles.Headless.Properties;
using NineChronicles.RPC.Shared.Exceptions;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using StrictRenderer =
    Libplanet.Blockchain.Renderers.Debug.ValidatingActionRenderer<Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>>;

namespace NineChronicles.Headless
{
    public class NineChroniclesNodeService : IHostedService, IDisposable
    {
        private LibplanetNodeService<NineChroniclesActionType> NodeService { get; set; }

        private LibplanetNodeServiceProperties<NineChroniclesActionType> Properties { get; }

        public BlockRenderer BlockRenderer { get; }

        public ActionRenderer ActionRenderer { get; }

        public ExceptionRenderer ExceptionRenderer { get; }

        public NodeStatusRenderer NodeStatusRenderer { get; }

        public AsyncManualResetEvent BootstrapEnded => NodeService.BootstrapEnded;

        public AsyncManualResetEvent PreloadEnded => NodeService.PreloadEnded;

        public Swarm<NineChroniclesActionType> Swarm => NodeService.Swarm;

        public BlockChain<NineChroniclesActionType> BlockChain => NodeService.BlockChain;

        public IStore Store => NodeService.Store;

        public PrivateKey? MinerPrivateKey { get; set; }

        static NineChroniclesNodeService()
        {
            try
            {
                Libplanet.Crypto.CryptoConfig.CryptoBackend = new Secp256K1CryptoBackend<SHA256>();
                Log.Debug("Secp256K1CryptoBackend initialized.");
            }
            catch (Exception e)
            {
                Log.Error("Secp256K1CryptoBackend initialize failed. Use default backend. {e}", e);
            }
        }

        public NineChroniclesNodeService(
            PrivateKey? minerPrivateKey,
            LibplanetNodeServiceProperties<NineChroniclesActionType> properties,
            Progress<PreloadState>? preloadProgress = null,
            bool ignoreBootstrapFailure = false,
            bool ignorePreloadFailure = false,
            bool strictRendering = false,
            bool authorizedMiner = false,
            bool isDev = false,
            int blockInterval = 10000,
            int reorgInterval = 0,
            TimeSpan txLifeTime = default
        )
        {
            MinerPrivateKey = minerPrivateKey;
            Properties = properties;

            LogEventLevel logLevel = LogEventLevel.Debug;
            var blockPolicySource = new BlockPolicySource(Log.Logger, logLevel);
            // BlockPolicy shared through Lib9c.
            IBlockPolicy<NineChroniclesActionType>? blockPolicy = null;
            // Policies for dev mode.
            IBlockPolicy<NineChroniclesActionType>? easyPolicy = null;
            IBlockPolicy<NineChroniclesActionType>? hardPolicy = null;
            IStagePolicy<NineChroniclesActionType> stagePolicy =
                txLifeTime == default
                    ? new VolatileStagePolicy<NineChroniclesActionType>()
                    : new VolatileStagePolicy<NineChroniclesActionType>(txLifeTime);
            if (isDev)
            {
                easyPolicy = new ReorgPolicy(new RewardGold(), 1);
                hardPolicy = new ReorgPolicy(new RewardGold(), 2);
            }
            else
            {
                blockPolicy = blockPolicySource.GetPolicy(properties.MinimumDifficulty, properties.MaximumTransactions);
            }

            BlockRenderer = blockPolicySource.BlockRenderer;
            ActionRenderer = blockPolicySource.ActionRenderer;
            ExceptionRenderer = new ExceptionRenderer();
            NodeStatusRenderer = new NodeStatusRenderer();
            var renderers = new List<IRenderer<NineChroniclesActionType>>();
            var strictRenderer = new StrictRenderer(onError: exc =>
                ExceptionRenderer.RenderException(
                    RPCException.InvalidRenderException,
                    exc.Message.Split("\n")[0]
                )
            );
            if (Properties.Render)
            {
                renderers.Add(blockPolicySource.BlockRenderer);
                renderers.Add(blockPolicySource.LoggedActionRenderer);
            }
            else if (Properties.LogActionRenders)
            {
                renderers.Add(blockPolicySource.BlockRenderer);
                // The following "nullRenderer" does nothing.  It's just for filling
                // the LoggedActionRenderer<T>() constructor's parameter:
                IActionRenderer<NineChroniclesActionType> nullRenderer =
                    new AnonymousActionRenderer<NineChroniclesActionType>();
                renderers.Add(
                    new LoggedActionRenderer<NineChroniclesActionType>(
                        nullRenderer,
                        Log.Logger,
                        logLevel
                    )
                );
            }
            else
            {
                renderers.Add(blockPolicySource.LoggedBlockRenderer);
            }

            if (strictRendering)
            {
                Log.Debug(
                    $"Strict rendering is on. Add {nameof(StrictRenderer)}.");
                renderers.Add(strictRenderer);
            }

            async Task minerLoopAction(
                BlockChain<NineChroniclesActionType> chain,
                Swarm<NineChroniclesActionType> swarm,
                PrivateKey privateKey,
                CancellationToken cancellationToken)
            {
                var miner = new Miner(chain, swarm, privateKey, authorizedMiner);
                Log.Debug("Miner called.");
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        long nextBlockIndex = chain.Tip.Index + 1;
                        bool isTargetBlock = blockPolicy is BlockPolicy bp
                                             // Copied from https://git.io/JLxNd
                                             && nextBlockIndex > 0
                                             && nextBlockIndex <= bp.AuthorizedMinersState?.ValidUntil
                                             && nextBlockIndex % bp.AuthorizedMinersState?.Interval == 0;
                        if (swarm.Running && (!authorizedMiner || isTargetBlock))
                        {
                            Log.Debug("Start mining.");
                            await miner.MineBlockAsync(properties.MaximumTransactions, cancellationToken);
                        }
                        else
                        {
                            await Task.Delay(1000, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred.");
                    }
                }
            }

            async Task devMinerLoopAction(
                Swarm<NineChroniclesActionType> mainSwarm,
                Swarm<NineChroniclesActionType> subSwarm,
                PrivateKey privateKey,
                CancellationToken cancellationToken)
            {
                var miner = new ReorgMiner(mainSwarm, subSwarm, privateKey, reorgInterval);
                Log.Debug("Miner called.");
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (mainSwarm.Running)
                        {
                            Log.Debug("Start mining.");
                            await miner.MineBlockAsync(properties.MaximumTransactions, cancellationToken);
                            await Task.Delay(blockInterval, cancellationToken);
                        }
                        else
                        {
                            await Task.Delay(1000, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred.");
                    }
                }
            }

            if (isDev)
            {
                NodeService = new DevLibplanetNodeService<NineChroniclesActionType>(
                    Properties,
                    easyPolicy,
                    hardPolicy,
                    stagePolicy,
                    renderers,
                    devMinerLoopAction,
                    preloadProgress,
                    (code, msg) =>
                    {
                        ExceptionRenderer.RenderException(code, msg);
                        Log.Error(msg);
                    },
                    isPreloadStarted => { NodeStatusRenderer.PreloadStatus(isPreloadStarted); },
                    ignoreBootstrapFailure
                );
            }
            else
            {
                NodeService = new LibplanetNodeService<NineChroniclesActionType>(
                    Properties,
                    blockPolicy,
                    stagePolicy,
                    renderers,
                    minerLoopAction,
                    preloadProgress,
                    (code, msg) =>
                    {
                        ExceptionRenderer.RenderException(code, msg);
                        Log.Error(msg);
                    },
                    isPreloadStarted =>
                    {
                        NodeStatusRenderer.PreloadStatus(isPreloadStarted);
                    },
                    ignoreBootstrapFailure,
                    ignorePreloadFailure
                );
            }

            strictRenderer.BlockChain = NodeService.BlockChain ?? throw new Exception("BlockChain is null.");
            if (NodeService.BlockChain?.GetState(AuthorizedMinersState.Address) is Dictionary ams &&
                blockPolicy is BlockPolicy bp)
            {
                bp.AuthorizedMinersState = new AuthorizedMinersState(ams);
            }

            if (authorizedMiner && blockPolicy is BlockPolicy {AuthorizedMinersState: null})
            {
                throw new Exception(
                    "--authorized-miner was set but there are no AuthorizedMinerState.");
            }
        }

        public static NineChroniclesNodeService Create(
            NineChroniclesNodeServiceProperties properties, 
            StandaloneContext context
        )
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Progress<PreloadState> progress = new Progress<PreloadState>(state =>
            {
                context.PreloadStateSubject.OnNext(state);
            });

            if (properties.Libplanet is null)
            {
                throw new InvalidOperationException($"{nameof(properties.Libplanet)} is null.");
            }

            properties.Libplanet.DifferentAppProtocolVersionEncountered =
                (Peer peer, AppProtocolVersion peerVersion, AppProtocolVersion localVersion) =>
                {
                    context.DifferentAppProtocolVersionEncounterSubject.OnNext(
                        new DifferentAppProtocolVersionEncounter(peer, peerVersion, localVersion)
                    );

                    // FIXME: 일단은 버전이 다른 피어는 마주쳐도 쌩깐다.
                    return false;
                };

            properties.Libplanet.NodeExceptionOccurred =
                (code, message) =>
                {
                    context.NodeExceptionSubject.OnNext(
                        new NodeException(code, message)
                    );
                };

            var service = new NineChroniclesNodeService(
                properties.MinerPrivateKey,
                properties.Libplanet,
                preloadProgress: progress,
                ignoreBootstrapFailure: properties.IgnoreBootstrapFailure,
                ignorePreloadFailure: properties.IgnorePreloadFailure,
                strictRendering: properties.StrictRender,
                isDev: properties.Dev,
                blockInterval: properties.BlockInterval,
                reorgInterval: properties.ReorgInterval,
                authorizedMiner: properties.AuthorizedMiner,
                txLifeTime: properties.TxLifeTime);
            service.ConfigureContext(context);
            return service;
        }

        internal static IBlockPolicy<NineChroniclesActionType> GetBlockPolicy(int minimumDifficulty, int maximumTransactions) =>
            new BlockPolicySource(Log.Logger, LogEventLevel.Debug)
                .GetPolicy(minimumDifficulty, maximumTransactions);

        public void StartMining() => NodeService?.StartMining(MinerPrivateKey);

        public void StopMining() => NodeService?.StopMining();
        
        public Task<bool> CheckPeer(string addr) => NodeService?.CheckPeer(addr) ?? throw new InvalidOperationException();

        public Task StartAsync(CancellationToken cancellationToken) 
        {
            if (!Properties.NoMiner)
            {
                StartMining();
            }

            return NodeService.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => NodeService.StopAsync(cancellationToken);

        public void Dispose()
        {
            NodeService?.Dispose();
        }

        internal void ConfigureContext(StandaloneContext standaloneContext)
        {
            standaloneContext.NineChroniclesNodeService = this;
            standaloneContext.BlockChain = Swarm.BlockChain;
            standaloneContext.Store = Store;
            BootstrapEnded.WaitAsync().ContinueWith((task) =>
            {
                standaloneContext.BootstrapEnded = true;
                standaloneContext.NodeStatusSubject.OnNext(standaloneContext.NodeStatus);
            });
            PreloadEnded.WaitAsync().ContinueWith((task) =>
            {
                standaloneContext.PreloadEnded = true;
                standaloneContext.NodeStatusSubject.OnNext(standaloneContext.NodeStatus);
            });
        }
    }
}
