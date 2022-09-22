using Libplanet.Net;

namespace NineChronicles.Headless
{
    public class DifferentAppProtocolVersionEncounter
    {
        public BoundPeer Peer { get; }

        public AppProtocolVersion PeerVersion { get; }

        public AppProtocolVersion LocalVersion { get; }

        public DifferentAppProtocolVersionEncounter(
            BoundPeer peer,
            AppProtocolVersion peerVersion,
            AppProtocolVersion localVersion)
        {
            Peer = peer;
            PeerVersion = peerVersion;
            LocalVersion = localVersion;
        }
    }
}
