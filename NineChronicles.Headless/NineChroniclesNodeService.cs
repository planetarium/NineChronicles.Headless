using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Lib9c.Renderers;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Crypto;
using Libplanet.Headless;
using Libplanet.Headless.Hosting;
using Libplanet.Net;
using Libplanet.Store;
using Microsoft.Extensions.Hosting;
using Nekoyume.Blockchain;
using Nekoyume.Blockchain.Policy;
using NineChronicles.Headless.Properties;
using NineChronicles.Headless.Utils;
using NineChronicles.Headless.Services;
using NineChronicles.RPC.Shared.Exceptions;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using StrictRenderer = Libplanet.Blockchain.Renderers.Debug.ValidatingActionRenderer;

namespace NineChronicles.Headless
{
    public class NineChroniclesNodeService : IHostedService, IDisposable
    {
        private LibplanetNodeService NodeService { get; set; }

        private LibplanetNodeServiceProperties Properties { get; }

        public BlockRenderer BlockRenderer { get; }

        public ActionRenderer ActionRenderer { get; }

        public ExceptionRenderer ExceptionRenderer { get; }

        public NodeStatusRenderer NodeStatusRenderer { get; }

        public AsyncManualResetEvent BootstrapEnded => NodeService.BootstrapEnded;

        public AsyncManualResetEvent PreloadEnded => NodeService.PreloadEnded;

        public Swarm Swarm => NodeService.Swarm;

        public BlockChain BlockChain => NodeService.BlockChain;

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
            LibplanetNodeServiceProperties properties,
            IBlockPolicy blockPolicy,
            NetworkType networkType,
            IActionLoader actionLoader,
            Progress<BlockSyncState>? preloadProgress = null,
            bool ignoreBootstrapFailure = false,
            bool ignorePreloadFailure = false,
            bool strictRendering = false,
            TimeSpan txLifeTime = default,
            int txQuotaPerSigner = 10,
            AccessControlServiceOptions? acsOptions = null
        )
        {
            MinerPrivateKey = minerPrivateKey;
            Properties = properties;

            LogEventLevel logLevel = LogEventLevel.Debug;

            IAccessControlService? accessControlService = null;

            if (acsOptions != null)
            {
                accessControlService = AccessControlServiceFactory.Create(
                    acsOptions.GetStorageType(),
                    acsOptions.AccessControlServiceConnectionString
                );
            }

            IStagePolicy stagePolicy = new NCStagePolicy(
                txLifeTime, txQuotaPerSigner, accessControlService);

            BlockRenderer = new BlockRenderer();
            ActionRenderer = new ActionRenderer();
            ExceptionRenderer = new ExceptionRenderer();
            NodeStatusRenderer = new NodeStatusRenderer();
            var renderers = new List<IRenderer>();
            var strictRenderer = new StrictRenderer(onError: exc =>
                ExceptionRenderer.RenderException(
                    RPCException.InvalidRenderException,
                    exc.Message.Split("\n")[0]
                )
            );

            if (Properties.Render)
            {
                renderers.Add(BlockRenderer);
                renderers.Add(new LoggedActionRenderer(ActionRenderer, Log.Logger, logLevel));
            }
            else if (Properties.LogActionRenders)
            {
                renderers.Add(BlockRenderer);
                // The following "nullRenderer" does nothing.  It's just for filling
                // the LoggedActionRenderer<T>() constructor's parameter:
                IActionRenderer nullRenderer = new AnonymousActionRenderer();
                renderers.Add(
                    new LoggedActionRenderer(
                        nullRenderer,
                        Log.Logger,
                        logLevel
                    )
                );
            }
            else
            {
                renderers.Add(new LoggedRenderer(BlockRenderer, Log.Logger, logLevel));
            }

            if (strictRendering)
            {
                Log.Debug(
                    $"Strict rendering is on. Add {nameof(StrictRenderer)}.");
                renderers.Add(strictRenderer);
            }

            NodeService = new LibplanetNodeService(
                Properties,
                blockPolicy,
                stagePolicy,
                renderers,
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
                actionLoader,
                ignoreBootstrapFailure,
                ignorePreloadFailure
            );
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

            Progress<BlockSyncState> progress = new Progress<BlockSyncState>(state =>
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
                properties.ActionLoader);
            var service = new NineChroniclesNodeService(
                properties.MinerPrivateKey,
                properties.Libplanet,
                blockPolicy,
                properties.NetworkType,
                properties.ActionLoader,
                preloadProgress: progress,
                ignoreBootstrapFailure: properties.IgnoreBootstrapFailure,
                ignorePreloadFailure: properties.IgnorePreloadFailure,
                strictRendering: properties.StrictRender,
                txLifeTime: properties.TxLifeTime,
                txQuotaPerSigner: properties.TxQuotaPerSigner,
                acsOptions: properties.AccessControlServiceOptions
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
            meter.CreateObservableGauge(
                "ninechronicles_tx_count",
                () => service.BlockChain.Tip.Transactions.Count,
                description: "The count of the tip block's transactions.");
            meter.CreateObservableGauge(
                "ninechronicles_block_interval",
                () =>
                {
                    var currentBlockHash = service.BlockChain.Tip.Hash;
                    var nullablePreviousBlockHash = service.BlockChain[currentBlockHash].PreviousHash;
                    if (nullablePreviousBlockHash is { } previousBlockHash)
                    {
                        return (service.BlockChain[currentBlockHash].Timestamp -
                                service.BlockChain[previousBlockHash].Timestamp).TotalSeconds;
                    }

                    // When the tip is genesis block...
                    return 0;
                },
                description: "The block interval between tip block and tip - 1 block.");

            return service;
        }

        internal static IBlockPolicy GetBlockPolicy(NetworkType networkType, IActionLoader actionLoader)
        {
            var source = new BlockPolicySource(actionLoader);
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

        internal static IBlockPolicy GetTestBlockPolicy() =>
            new BlockPolicySource().GetTestPolicy();

        public Task<bool> CheckPeer(string addr) => NodeService?.CheckPeer(addr) ?? throw new InvalidOperationException();

        public Task StartAsync(CancellationToken cancellationToken)
        {
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
            standaloneContext.Swarm = Swarm;
            standaloneContext.CurrencyFactory =
                new CurrencyFactory(() => standaloneContext.BlockChain.GetWorldState(standaloneContext.BlockChain.Tip.Hash));
            standaloneContext.FungibleAssetValueFactory =
                new FungibleAssetValueFactory(standaloneContext.CurrencyFactory);
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
