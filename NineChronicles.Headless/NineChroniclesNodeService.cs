using Lib9c.Renderers;
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
using Nekoyume.BlockChain.Policy;
using Nekoyume.Model.State;
using NineChronicles.Headless.Properties;
using NineChronicles.RPC.Shared.Exceptions;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using StrictRenderer =
    Libplanet.Blockchain.Renderers.Debug.ValidatingActionRenderer<Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>>;
using Libplanet.Blocks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;

namespace NineChronicles.Headless
{
    public class NineChroniclesNodeService : IHostedService, IDisposable
    {
        public LibplanetNodeService<NCAction> NodeService { get; private set; }

        private LibplanetNodeServiceProperties<NCAction> Properties { get; }

        public BlockRenderer BlockRenderer { get; }

        public ActionRenderer ActionRenderer { get; }

        public ExceptionRenderer ExceptionRenderer { get; }

        public NodeStatusRenderer NodeStatusRenderer { get; }

        public AsyncManualResetEvent BootstrapEnded => NodeService.BootstrapEnded;

        public AsyncManualResetEvent PreloadEnded => NodeService.PreloadEnded;

        public Swarm<NCAction> Swarm => NodeService.Swarm;

        public BlockChain<NCAction> BlockChain => NodeService.BlockChain;

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
            LibplanetNodeServiceProperties<NCAction> properties,
            IBlockPolicy<NCAction> blockPolicy,
            NetworkType networkType,
            IActionTypeLoader actionTypeLoader,
            TimeSpan? minerBlockInterval = null,
            Progress<PreloadState>? preloadProgress = null,
            bool ignoreBootstrapFailure = false,
            bool ignorePreloadFailure = false,
            bool strictRendering = false,
            TimeSpan txLifeTime = default,
            int minerCount = 1,
            int txQuotaPerSigner = 10
        )
        {
            MinerPrivateKey = minerPrivateKey;
            Properties = properties;

            LogEventLevel logLevel = LogEventLevel.Debug;
            var blockPolicySource = new BlockPolicySource(Log.Logger, logLevel);
            IStagePolicy<NCAction> stagePolicy = new StagePolicy(txLifeTime, txQuotaPerSigner);

            BlockRenderer = blockPolicySource.BlockRenderer;
            ActionRenderer = blockPolicySource.ActionRenderer;
            ExceptionRenderer = new ExceptionRenderer();
            NodeStatusRenderer = new NodeStatusRenderer();
            var renderers = new List<IRenderer<NCAction>>();
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
                IActionRenderer<NCAction> nullRenderer =
                    new AnonymousActionRenderer<NCAction>();
                renderers.Add(
                    new LoggedActionRenderer<NCAction>(
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
                BlockChain<NCAction> chain,
                Swarm<NCAction> swarm,
                PrivateKey privateKey,
                CancellationToken cancellationToken)
            {
                var miner = new Miner(chain, swarm, privateKey, actionTypeLoader);
                Log.Debug("Miner called.");
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        long nextBlockIndex = chain.Tip.Index + 1;

                        if (swarm.Running)
                        {
                            Log.Debug("Start mining.");

                            if (chain.Policy is BlockPolicy bp)
                            {
                                if (bp.IsAllowedToMine(privateKey.ToAddress(), chain.Count))
                                {
                                    IEnumerable<Task<Block<NCAction>?>> miners = Enumerable
                                        .Range(0, minerCount)
                                        .Select(_ => miner.MineBlockAsync(cancellationToken));
                                    await Task.WhenAll(miners);
                                    await Task.Delay(minerBlockInterval ?? TimeSpan.Zero);
                                }
                                else
                                {
                                    Log.Debug(
                                        "Miner {MinerAddress} is not allowed to mine a block with index {Index} " +
                                        "under current policy.",
                                        privateKey.ToAddress(),
                                        chain.Count);
                                    await Task.Delay(1000, cancellationToken);
                                }
                            }
                            else
                            {
                                Log.Error(
                                    "No suitable policy was found for chain {ChainId}.",
                                    chain.Id);
                                await Task.Delay(1000, cancellationToken);
                            }
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

            NodeService = new LibplanetNodeService<NCAction>(
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
                actionTypeLoader,
                ignoreBootstrapFailure,
                ignorePreloadFailure
            );

            strictRenderer.BlockChain = NodeService.BlockChain ?? throw new Exception("BlockChain is null.");
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
                (BoundPeer peer, AppProtocolVersion peerVersion, AppProtocolVersion localVersion) =>
                {
                    context.DifferentAppProtocolVersionEncounterSubject.OnNext(
                        new DifferentAppProtocolVersionEncounter(peer, peerVersion, localVersion)
                    );
                };

            properties.Libplanet.NodeExceptionOccurred =
                (code, message) =>
                {
                    context.NodeExceptionSubject.OnNext(
                        new NodeException(code, message)
                    );
                };

            var blockPolicy = NineChroniclesNodeService.GetBlockPolicy(
                properties.NetworkType,
                properties.ActionTypeLoader);
            var service = new NineChroniclesNodeService(
                properties.MinerPrivateKey,
                properties.Libplanet,
                blockPolicy,
                properties.NetworkType,
                properties.ActionTypeLoader,
                properties.MinerBlockInterval,
                preloadProgress: progress,
                ignoreBootstrapFailure: properties.IgnoreBootstrapFailure,
                ignorePreloadFailure: properties.IgnorePreloadFailure,
                strictRendering: properties.StrictRender,
                txLifeTime: properties.TxLifeTime,
                minerCount: properties.MinerCount,
                txQuotaPerSigner: properties.TxQuotaPerSigner
            );
            service.ConfigureContext(context);
            var meter = new Meter("NineChronicles");
            meter.CreateObservableGauge(
                "ninechronicles_tip_index",
                () => service.BlockChain.Tip.Index,
                description: "The tip block's index.");
            meter.CreateObservableGauge(
                "ninechronicles_staged_txids_count",
                () => service.BlockChain.GetStagedTransactionIds().Count,
                description: "Number of staged transactions.");
            meter.CreateObservableGauge(
                "ninechronicles_subscriber_addresses_count",
                () => context.AgentAddresses.Count);

            return service;
        }

        internal static IBlockPolicy<NCAction> GetBlockPolicy(NetworkType networkType, IActionTypeLoader actionTypeLoader)
        {
            var source = new BlockPolicySource(Log.Logger, LogEventLevel.Debug, actionTypeLoader);
            return networkType switch
            {
                NetworkType.Main => source.GetPolicy(),
                NetworkType.Internal => source.GetInternalPolicy(),
                NetworkType.Permanent => source.GetPermanentPolicy(),
                NetworkType.Test => source.GetTestPolicy(),
                NetworkType.Default => source.GetDefaultPolicy(),
                _ => throw new ArgumentOutOfRangeException(nameof(networkType), networkType, null),
            };
        }

        internal static IBlockPolicy<NCAction> GetTestBlockPolicy() =>
            new BlockPolicySource(Log.Logger, LogEventLevel.Debug).GetTestPolicy();

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
