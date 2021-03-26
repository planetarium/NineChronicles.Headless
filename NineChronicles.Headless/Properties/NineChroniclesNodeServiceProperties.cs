using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using NineChronicles.Headless.Exceptions;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

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
                int demandBuffer = 1150)
        {
            var swarmPrivateKey = string.IsNullOrEmpty(swarmPrivateKeyString)
                ? new PrivateKey()
                : new PrivateKey(ByteUtil.ParseHex(swarmPrivateKeyString));

            peerStrings ??= Array.Empty<string>();
            iceServerStrings ??= Array.Empty<string>();

            var iceServers = iceServerStrings.Select(LoadIceServer).ToImmutableArray();
            var peers = peerStrings.Select(LoadPeer).ToImmutableArray();

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
                DemandBuffer = demandBuffer
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

        private static IceServer LoadIceServer(string iceServerInfo)
        {
            try
            {
                var uri = new Uri(iceServerInfo);
                string[] userInfo = uri.UserInfo.Split(':');

                return new IceServer(new[] {uri}, userInfo[0], userInfo[1]);
            }
            catch (Exception e)
            {
                throw new IceServerInvalidException(
                    $"--ice-server '{iceServerInfo}' seems invalid.\n" +
                    $"{e.GetType()} {e.Message}\n" +
                    $"{e.StackTrace}", innerException: e);
            }
        }

        private static BoundPeer LoadPeer(string peerInfo)
        {
            var tokens = peerInfo.Split(',');
            if (tokens.Length != 3)
            {
                throw new PeerInvalidException(
                    $"--peer '{peerInfo}', should have format <pubkey>,<host>,<port>");
            }

            if (!(tokens[0].Length == 130 || tokens[0].Length == 66))
            {
                throw new PeerInvalidException(
                    $"--peer '{peerInfo}', a length of public key must be 130 or 66 in hexadecimal," +
                    $" but the length of given public key '{tokens[0]}' doesn't.");
            }

            try
            {
                var pubKey = new PublicKey(ByteUtil.ParseHex(tokens[0]));
                var host = tokens[1];
                var port = int.Parse(tokens[2]);

                // FIXME: It might be better to make Peer.AppProtocolVersion property nullable...
                return new BoundPeer(
                    pubKey,
                    new DnsEndPoint(host, port));
            }
            catch (Exception e)
            {
                throw new PeerInvalidException(
                    $"--peer '{peerInfo}' seems invalid.\n" +
                    $"{e.GetType()} {e.Message}\n" +
                    $"{e.StackTrace}", innerException: e);
            }
        }
    }
}
