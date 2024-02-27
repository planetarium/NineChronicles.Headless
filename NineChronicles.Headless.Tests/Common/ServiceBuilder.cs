using System;
using Libplanet.Types.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using System.Collections.Immutable;
using System.IO;
using Libplanet.Blockchain.Policies;
using NineChronicles.Headless.Properties;
using Nekoyume.Blockchain.Policy;
using Nekoyume;

namespace NineChronicles.Headless.Tests.Common
{
    public static class ServiceBuilder
    {
        public const int MinimumDifficulty = 4096;

        public const int MaximumTransactions = 100;

        public static IBlockPolicy BlockPolicy =>
            new BlockPolicySource().GetPolicy();

        public static NineChroniclesNodeService CreateNineChroniclesNodeService(
            Block genesis,
            PrivateKey? privateKey = null
        )
        {
            var storePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var properties = new LibplanetNodeServiceProperties
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = storePath,
                StoreStatesCacheSize = 2,
                SwarmPrivateKey = new PrivateKey(),
                ConsensusPrivateKey = privateKey,
                Port = null,
                ConsensusPort = null,
                NoMiner = true,
                Render = false,
                LogActionRenders = false,
                Peers = ImmutableHashSet<BoundPeer>.Empty,
                TrustedAppProtocolVersionSigners = null,
                MessageTimeout = TimeSpan.FromMinutes(1),
                TipTimeout = TimeSpan.FromMinutes(1),
                DemandBuffer = 1150,
                ConsensusSeeds = ImmutableList<BoundPeer>.Empty,
                ConsensusPeers = ImmutableList<BoundPeer>.Empty,
                IceServers = ImmutableList<IceServer>.Empty,
            };
            return new NineChroniclesNodeService(
                privateKey,
                properties,
                BlockPolicy,
                Planet.Odin,
                StaticActionLoaderSingleton.Instance);
        }
    }
}
