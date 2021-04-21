using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
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
        /// A private key that is used in mining and signing transactions, which is different
        /// with the private key used in swarm to sign messages.
        /// </summary>
        /// <seealso cref="LibplanetNodeServiceProperties{T}.SwarmPrivateKey"/>
        public PrivateKey? MinerPrivateKey { get; set; }
        
        public RpcNodeServiceProperties? Rpc { get; set; }

        public LibplanetNodeServiceProperties<NineChroniclesActionType>? Libplanet { get; set; }

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
                string[]? staticPeerStrings = null)
        {
            var swarmPrivateKey = string.IsNullOrEmpty(swarmPrivateKeyString)
                ? new PrivateKey()
                : new PrivateKey(ByteUtil.ParseHex(swarmPrivateKeyString));

            peerStrings ??= Array.Empty<string>();
            iceServerStrings ??= Array.Empty<string>();
            staticPeerStrings ??= Array.Empty<string>();

            var iceServers = iceServerStrings.Select(PropertyParser.ParseIceServer).ToImmutableArray();
            var peers = peerStrings.Select(PropertyParser.ParsePeer).ToImmutableArray();
            var staticPeers = staticPeerStrings.Select(PropertyParser.ParsePeer).ToImmutableArray();

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
                StaticPeers = staticPeers
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
