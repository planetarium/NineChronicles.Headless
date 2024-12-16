using System;
using System.Collections.Immutable;
using System.Linq;
using Libplanet.Action.Loader;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Libplanet.Headless;
using Libplanet.Net.Consensus;
using Nekoyume;

namespace NineChronicles.Headless.Properties
{
    public class NineChroniclesNodeServiceProperties
    {
        public NineChroniclesNodeServiceProperties(
            IActionLoader actionLoader, AccessControlServiceOptions? accessControlServiceOptions)
        {
            ActionLoader = actionLoader;
            AccessControlServiceOptions = accessControlServiceOptions;
        }

        /// <summary>
        /// Gets or sets a private key that is used in mining and signing transactions,
        /// which is different with the private key used in swarm to sign messages.
        /// </summary>
        /// <seealso cref="LibplanetNodeServiceProperties{T}.SwarmPrivateKey"/>
        public PrivateKey? MinerPrivateKey { get; set; }

        public LibplanetNodeServiceProperties? Libplanet { get; set; }

        public Planet Planet { get; set; } = Planet.Odin;

        // FIXME: Replaced by NetworkType.Dev (not exist yet).
        public bool Dev { get; set; }

        public bool StrictRender { get; set; }

        public int BlockInterval { get; set; }

        public int ReorgInterval { get; set; }

        public TimeSpan TxLifeTime { get; set; }

        public bool IgnoreBootstrapFailure { get; set; } = true;

        public bool IgnorePreloadFailure { get; set; } = true;

        public int MinerCount { get; set; }

        public TimeSpan MinerBlockInterval { get; set; } = TimeSpan.Zero;

        public int TxQuotaPerSigner { get; set; }

        public int? MaxTransactionPerBlock { get; set; }

        public IActionLoader ActionLoader { get; init; }

        public AccessControlServiceOptions? AccessControlServiceOptions { get; }

        public static LibplanetNodeServiceProperties
            GenerateLibplanetNodeServiceProperties(
                string? appProtocolVersionToken = null,
                string? genesisBlockPath = null,
                string? swarmHost = null,
                ushort? swarmPort = null,
                string? swarmPrivateKeyString = null,
                string? storeType = null,
                string? storePath = null,
                bool noReduceStore = false,
                int storeStateCacheSize = 100,
                string[]? iceServerStrings = null,
                string[]? peerStrings = null,
                string[]? trustedAppProtocolVersionSigners = null,
                bool noMiner = false,
                bool render = false,
                int workers = 5,
                int confirmations = 0,
                bool nonblockRenderer = false,
                int nonblockRendererQueue = 512,
                int messageTimeout = 60,
                int tipTimeout = 60,
                int demandBuffer = 1150,
                bool preload = true,
                int minimumBroadcastTarget = 10,
                int bucketSize = 16,
                string chainTipStaleBehaviorType = "reboot",
                int maximumPollPeers = int.MaxValue,
                ushort? consensusPort = null,
                string? consensusPrivateKeyString = null,
                string[]? consensusSeedStrings = null,
                double? consensusTargetBlockIntervalMilliseconds = null,
                int? consensusProposeTimeoutBase = null,
                int? consensusEnterPreCommitDelay = null,
                IActionEvaluatorConfiguration? actionEvaluatorConfiguration = null)
        {
            var swarmPrivateKey = string.IsNullOrEmpty(swarmPrivateKeyString)
                ? new PrivateKey()
                : new PrivateKey(ByteUtil.ParseHex(swarmPrivateKeyString));
            var consensusPrivateKey = string.IsNullOrEmpty(consensusPrivateKeyString)
                ? null
                : new PrivateKey(ByteUtil.ParseHex(consensusPrivateKeyString));

            peerStrings ??= Array.Empty<string>();
            iceServerStrings ??= Array.Empty<string>();

            var iceServers = iceServerStrings.Select(PropertyParser.ParseIceServer).ToImmutableArray();
            var peers = peerStrings.Select(PropertyParser.ParsePeer).ToImmutableArray();
            var consensusSeeds = consensusSeedStrings?.Select(PropertyParser.ParsePeer).ToImmutableList();

            var defaultContextOption = new ContextOption();
            var consensusContextOption = new ContextOption(
                proposeTimeoutBase: consensusProposeTimeoutBase ?? defaultContextOption.ProposeTimeoutBase,
                enterPreCommitDelay: consensusEnterPreCommitDelay ?? defaultContextOption.EnterPreCommitDelay);

            return new LibplanetNodeServiceProperties
            {
                Host = swarmHost,
                Port = swarmPort,
                SwarmPrivateKey = swarmPrivateKey,
                AppProtocolVersion = AppProtocolVersion.FromToken(
                    appProtocolVersionToken ??
                        throw new InvalidOperationException("appProtocolVersionToken cannot be null.")),
                TrustedAppProtocolVersionSigners = trustedAppProtocolVersionSigners
                    ?.Select(s => new PublicKey(ByteUtil.ParseHex(s)))
                    ?.ToHashSet(),
                GenesisBlockPath = genesisBlockPath,
                NoMiner = noMiner,
                IceServers = iceServers,
                Peers = peers,
                StoreType = storeType,
                StorePath = storePath,
                NoReduceStore = noReduceStore,
                StoreStatesCacheSize = storeStateCacheSize,
                Render = render,
                Confirmations = Math.Max(confirmations, 0),
                NonblockRenderer = nonblockRenderer,
                NonblockRendererQueue = Math.Max(nonblockRendererQueue, 1),
                MessageTimeout = TimeSpan.FromSeconds(messageTimeout),
                TipTimeout = TimeSpan.FromSeconds(tipTimeout),
                DemandBuffer = demandBuffer,
                Preload = preload,
                MinimumBroadcastTarget = minimumBroadcastTarget,
                BucketSize = bucketSize,
                ChainTipStaleBehavior = chainTipStaleBehaviorType,
                MaximumPollPeers = maximumPollPeers,
                ConsensusPort = consensusPort,
                ConsensusSeeds = consensusSeeds,
                ConsensusPrivateKey = consensusPrivateKey,
                ConsensusTargetBlockIntervalMilliseconds = consensusTargetBlockIntervalMilliseconds,
                ContextOption = consensusContextOption,
                ActionEvaluatorConfiguration = actionEvaluatorConfiguration ?? new DefaultActionEvaluatorConfiguration(),
            };
        }

        public static RpcNodeServiceProperties GenerateRpcNodeServiceProperties(
            string rpcListenHost = "0.0.0.0",
            int? rpcListenPort = null,
            bool rpcRemoteServer = false)
        {

            if (string.IsNullOrEmpty(rpcListenHost))
            {
                throw new ArgumentException(
                    "--rpc-listen-host is required when --rpc-server is present.");
            }

            if (!(rpcListenPort is int rpcPortValue))
            {
                throw new ArgumentException(
                    "--rpc-listen-port is required when --rpc-server is present.");
            }

            return new RpcNodeServiceProperties
            {
                RpcListenHost = rpcListenHost,
                RpcListenPort = rpcPortValue,
                RpcRemoteServer = rpcRemoteServer,
            };
        }
    }
}
