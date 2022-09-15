using System;
using System.Collections.Generic;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Nekoyume.Action;
using System.Collections.Immutable;
using System.IO;
using Libplanet.Blockchain.Policies;
using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless.Tests.Common
{
    public static class ServiceBuilder
    {
        public const int MinimumDifficulty = 4096;

        public const int MaximumTransactions = 100;

        public static IBlockPolicy<PolymorphicAction<ActionBase>> BlockPolicy =>
            NineChroniclesNodeService.GetTestBlockPolicy();

        public static NineChroniclesNodeService CreateNineChroniclesNodeService(
            Block<PolymorphicAction<ActionBase>> genesis,
            PrivateKey? privateKey = null
        )
        {
            var consensusPrivateKey = new BlsPrivateKey();
            var storePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = storePath,
                StoreStatesCacheSize = 2,
                SwarmPrivateKey = new PrivateKey(),
                ConsensusPrivateKey = consensusPrivateKey,
                ConsensusPort = null,
                Validators = new List<BlsPublicKey>()
                {
                    consensusPrivateKey.PublicKey,
                },
                Port = null,
                NoMiner = true,
                Render = false,
                LogActionRenders = false,
                Peers = ImmutableHashSet<BoundPeer>.Empty,
                TrustedAppProtocolVersionSigners = null,
                MessageTimeout = TimeSpan.FromMinutes(1),
                TipTimeout = TimeSpan.FromMinutes(1),
                DemandBuffer = 1150,
                ConsensusPeers = ImmutableList<BoundPeer>.Empty,
            };
            return new NineChroniclesNodeService(privateKey, properties, BlockPolicy, NetworkType.Test);
        }
    }
}
