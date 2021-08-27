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
        
        public bool Dev { get; set; }
        
        public bool StrictRender { get; set; }
        
        public int BlockInterval { get; set; }
       
        public int ReorgInterval { get; set; }
        
        public bool AuthorizedMiner { get; set; }
        
        public TimeSpan TxLifeTime { get; set; }

        public bool IgnoreBootstrapFailure { get; set; } = true;

        public bool IgnorePreloadFailure { get; set; } = true;

        public int MinerCount { get; set; }


        public static LibplanetNodeServiceProperties<NineChroniclesActionType>
            GenerateLibplanetNodeServiceProperties(
                string? appProtocolVersionToken = null,
                string? genesisBlockPath = null,
                string? swarmHost = null,
                ushort? swarmPort = null,
                string? swarmPrivateKeyString = null,
                int minimumDifficulty = 5000000,
                string? storeType = null,
                string? storePath = null,
                int storeStateCacheSize = 100,
                string[]? iceServerStrings = null,
                string[]? peerStrings = null,
                string[]? trustedAppProtocolVersionSigners = null,
                bool noMiner = false,
                bool render = false,
                int workers = 5,
                int confirmations = 0,
                int maximumTransactions = 100,
                int messageTimeout = 60,
                int tipTimeout = 60,
                int demandBuffer = 1150,
                string[]? staticPeerStrings = null,
                bool preload = true,
                int minimumBroadcastTarget = 10,
                int bucketSize = 16,
                string chainTipStaleBehaviorType = "reboot",
                int pollInterval = 15,
                int maximumPollPeers = int.MaxValue)
        {
            var swarmPrivateKey = string.IsNullOrEmpty(swarmPrivateKeyString)
                ? new PrivateKey()
                : new PrivateKey(ByteUtil.ParseHex(swarmPrivateKeyString));

            peerStrings ??= Array.Empty<string>();
            iceServerStrings ??= Array.Empty<string>();
            staticPeerStrings ??= Array.Empty<string>();

            var iceServers = iceServerStrings.Select(PropertyParser.ParseIceServer).ToImmutableArray();
            var peers = peerStrings.Select(PropertyParser.ParsePeer).ToImmutableArray();
            var staticPeers = staticPeerStrings.Select(PropertyParser.ParsePeer).ToImmutableHashSet();

            return new LibplanetNodeServiceProperties<NineChroniclesActionType>
            {
                Host = swarmHost,
                Port = swarmPort,
                SwarmPrivateKey = swarmPrivateKey,
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
                StoreStatesCacheSize = storeStateCacheSize,
                MinimumDifficulty = minimumDifficulty,
                Render = render,
                Workers = workers,
                Confirmations = Math.Max(confirmations, 0),
                MaximumTransactions = maximumTransactions,
                MessageTimeout = TimeSpan.FromSeconds(messageTimeout),
                TipTimeout = TimeSpan.FromSeconds(tipTimeout),
                DemandBuffer = demandBuffer,
                StaticPeers = staticPeers,
                Preload = preload,
                MinimumBroadcastTarget = minimumBroadcastTarget,
                BucketSize = bucketSize,
                ChainTipStaleBehavior = chainTipStaleBehaviorType,
                PollInterval = TimeSpan.FromSeconds(pollInterval),
                MaximumPollPeers = maximumPollPeers
            };
        }

        public static RpcNodeServiceProperties GenerateRpcNodeServiceProperties(
            string rpcListenHost = "0.0.0.0",
            int? rpcListenPort = null)
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
                RpcListenPort = rpcPortValue
            };
        }
    }
}
