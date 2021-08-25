using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;

namespace Libplanet.Headless.Hosting
{
    public class LibplanetNodeServiceProperties<T>
        where T : IAction, new()
    {
        // swarm.
        public string Host { get; set; }

        public ushort? Port { get; set; }

        public PrivateKey SwarmPrivateKey { get; set; }

        public string StoreType { get; set; }

        public string StorePath { get; set; }

        public int StoreStatesCacheSize { get; set; }

        public string GenesisBlockPath { get; set; }

        public Block<T> GenesisBlock { get; set; }

        public IEnumerable<Peer> Peers { get; set; }

        public bool NoMiner { get; set; }

        public IEnumerable<IceServer> IceServers { get; set; }

        public AppProtocolVersion AppProtocolVersion { get; set; }

        public ISet<PublicKey> TrustedAppProtocolVersionSigners { get; set; }

        public int MinimumDifficulty { get; set; }

        public DifferentAppProtocolVersionEncountered DifferentAppProtocolVersionEncountered { get; set; }

        public bool Render { get; set; }

        public bool LogActionRenders { get; set; }

        public int Workers { get; set; } = 5;

        public int Confirmations { get; set; } = 0;

        public System.Action<NodeExceptionType, string> NodeExceptionOccurred { get; set; }

        public int MaximumTransactions { get; set; } = 100;

        public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public TimeSpan TipTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public int DemandBuffer { get; set; } = 1150;

        public ImmutableHashSet<BoundPeer> StaticPeers { get; set; }

        public bool Preload { get; set; } = true;

        public int MinimumBroadcastTarget { get; set; } = 10;

        public int BucketSize { get; set; } = 16;

        public string ChainTipStaleBehavior { get; set; } = "reboot";

        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

        public int MaximumPollPeers { get; set; } = int.MaxValue;
    }
}
