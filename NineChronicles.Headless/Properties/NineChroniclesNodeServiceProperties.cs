using System;
using System.Collections.Immutable;
using System.Linq;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Libplanet.Headless;

namespace NineChronicles.Headless.Properties
{
    public class NineChroniclesNodeServiceProperties
    {
        /// <summary>
        /// Gets or sets a private key that is used in mining and signing transactions,
        /// which is different with the private key used in swarm to sign messages.
        /// </summary>
        /// <seealso cref="LibplanetNodeServiceProperties{T}.SwarmPrivateKey"/>
        public PrivateKey? MinerPrivateKey { get; set; }

        public LibplanetNodeServiceProperties<NineChroniclesActionType>? Libplanet { get; set; }

        public NetworkType NetworkType { get; set; } = NetworkType.Main;

        public bool StrictRender { get; set; }

        public TimeSpan TxLifeTime { get; set; }

        public bool IgnoreBootstrapFailure { get; set; } = true;

        public bool IgnorePreloadFailure { get; set; } = true;

        public int TxQuotaPerSigner { get; set; }


        public static LibplanetNodeServiceProperties<NineChroniclesActionType>
            GenerateLibplanetNodeServiceProperties(
                string? appProtocolVersionToken = null,
                string? genesisBlockPath = null,
                string? swarmHost = null,
                ushort? swarmPort = null,
                ushort? consensusPort = null,
                string? swarmPrivateKeyString = null,
                string? consensusPrivateKeyString = null,
                string? minerPrivateKeyString = null,
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
                string[]? consensusPeerStrings = null,
                bool preload = true,
                int minimumBroadcastTarget = 10,
                int bucketSize = 16,
                string chainTipStaleBehaviorType = "reboot",
                int maximumPollPeers = int.MaxValue,
                int blockInterval = 10000,
                string[]? validatorStrings = null)
        {
            var swarmPrivateKey = string.IsNullOrEmpty(swarmPrivateKeyString)
                ? new PrivateKey()
                : new PrivateKey(ByteUtil.ParseHex(swarmPrivateKeyString));
            var consensusPrivateKey = string.IsNullOrEmpty(consensusPrivateKeyString)
                ? new BlsPrivateKey()
                : new BlsPrivateKey(ByteUtil.ParseHex(consensusPrivateKeyString));
            var minerPrivateKey = string.IsNullOrEmpty(minerPrivateKeyString)
                ? new PrivateKey()
                : new PrivateKey(ByteUtil.ParseHex(minerPrivateKeyString));

            peerStrings ??= Array.Empty<string>();
            iceServerStrings ??= Array.Empty<string>();
            consensusPeerStrings ??= Array.Empty<string>();

            var iceServers = iceServerStrings.Select(PropertyParser.ParseIceServer).ToImmutableArray();
            var peers = peerStrings.Select(PropertyParser.ParsePeer).ToImmutableArray();
            var consensusPeers = consensusPeerStrings.Select(PropertyParser.ParsePeer).ToImmutableList();
            var validators = validatorStrings?.Select(s => new BlsPublicKey(ByteUtil.ParseHex(s))).ToList();

            return new LibplanetNodeServiceProperties<NineChroniclesActionType>
            {
                Host = swarmHost,
                Port = swarmPort,
                ConsensusPort = consensusPort,
                MinerPrivateKey = minerPrivateKey,
                SwarmPrivateKey = swarmPrivateKey,
                ConsensusPrivateKey = consensusPrivateKey,
                AppProtocolVersion = AppProtocolVersion.FromToken(appProtocolVersionToken),
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
                Workers = workers,
                Confirmations = Math.Max(confirmations, 0),
                NonblockRenderer = nonblockRenderer,
                NonblockRendererQueue = Math.Max(nonblockRendererQueue, 1),
                MessageTimeout = TimeSpan.FromSeconds(messageTimeout),
                TipTimeout = TimeSpan.FromSeconds(tipTimeout),
                DemandBuffer = demandBuffer,
                ConsensusPeers = consensusPeers,
                Preload = preload,
                MinimumBroadcastTarget = minimumBroadcastTarget,
                BucketSize = bucketSize,
                ChainTipStaleBehavior = chainTipStaleBehaviorType,
                MaximumPollPeers = maximumPollPeers,
                BlockInterval = blockInterval,
                Validators = validators,
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
