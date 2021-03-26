using Libplanet.Net;

namespace NineChronicles.Headless
{
    public class DifferentAppProtocolVersionEncounter
    {
        public Peer Peer { get; }

        public AppProtocolVersion PeerVersion { get; }

        public AppProtocolVersion LocalVersion { get; }

        public DifferentAppProtocolVersionEncounter(
            Peer peer,
            AppProtocolVersion peerVersion,
            AppProtocolVersion localVersion)
        {
            Peer = peer;
            PeerVersion = peerVersion;
            LocalVersion = localVersion;
        }
    }
}
