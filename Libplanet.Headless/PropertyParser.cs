using System;
using System.Net;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Net;

namespace Libplanet.Headless
{
    public static class PropertyParser
    {
        public static IceServer ParseIceServer(string iceServerInfo)
        {
            try
            {
                return new IceServer(new Uri(iceServerInfo));
            }
            catch (Exception e)
            {
                throw new IceServerInvalidException(
                    $"--ice-server '{iceServerInfo}' seems invalid.\n" +
                    $"{e.GetType()} {e.Message}\n" +
                    $"{e.StackTrace}", innerException: e);
            }
        }

        public static BoundPeer ParsePeer(string peerInfo)
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
