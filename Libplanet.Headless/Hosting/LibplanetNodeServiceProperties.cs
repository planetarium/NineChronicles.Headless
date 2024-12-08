using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Types.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Net.Consensus;

namespace Libplanet.Headless.Hosting
{
    public class LibplanetNodeServiceProperties
    {
        // swarm.
        public string Host { get; set; }

        public ushort? Port { get; set; }

        public ushort? ConsensusPort { get; set; }

        public double? ConsensusTargetBlockIntervalMilliseconds { get; set; }

        public PrivateKey SwarmPrivateKey { get; set; }

        public PrivateKey ConsensusPrivateKey { get; set; }

        public PrivateKey MinerPrivateKey { get; set; }

        public List<PublicKey> Validators { get; set; }

        public string StoreType { get; set; }

        public string StorePath { get; set; }

        public bool NoReduceStore { get; set; }

        public int StoreStatesCacheSize { get; set; }

        public string GenesisBlockPath { get; set; }

        public Block GenesisBlock { get; set; }

        public IEnumerable<BoundPeer> Peers { get; set; }

        public bool NoMiner { get; set; }

        public IEnumerable<IceServer> IceServers { get; set; }

        public AppProtocolVersion AppProtocolVersion { get; set; }

        public ISet<PublicKey> TrustedAppProtocolVersionSigners { get; set; }

        public DifferentAppProtocolVersionEncountered DifferentAppProtocolVersionEncountered { get; set; }

        public bool Render { get; set; }

        public bool LogActionRenders { get; set; }

        public int Confirmations { get; set; } = 0;

        public bool NonblockRenderer { get; set; } = false;

        public int NonblockRendererQueue { get; set; } = 512;

        public System.Action<NodeExceptionType, string> NodeExceptionOccurred { get; set; }

        public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public TimeSpan TipTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public ContextOption ContextOption { get; set; }

        public int DemandBuffer { get; set; } = 1150;

        public ImmutableList<BoundPeer> ConsensusSeeds { get; set; }

        public ImmutableList<BoundPeer> ConsensusPeers { get; set; }

        public bool Preload { get; set; } = true;

        public int MinimumBroadcastTarget { get; set; } = 10;

        public int BucketSize { get; set; } = 16;

        public string ChainTipStaleBehavior { get; set; } = "reboot";

        public int MaximumPollPeers { get; set; } = int.MaxValue;

#nullable enable
        public DynamicActionTypeLoaderConfiguration? DynamicActionTypeLoader { get; init; } = null;

        public IActionEvaluatorConfiguration ActionEvaluatorConfiguration { get; init; } =
            new DefaultActionEvaluatorConfiguration();
#nullable disable
    }
}
