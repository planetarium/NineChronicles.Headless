using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Standalone.Hosting;
using Nekoyume.Action;
using System.Collections.Immutable;

namespace NineChronicles.Standalone.Tests.Common
{
    public static class ServiceBuilder
    {
        public static NineChroniclesNodeService CreateNineChroniclesNodeService(
            Block<PolymorphicAction<ActionBase>> genesis
        )
        {
            var privateKey = new PrivateKey();
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = null,
                StoreStatesCacheSize = 2,
                PrivateKey = privateKey,
                Port = null,
                MinimumDifficulty = 4096,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
            };
            return new NineChroniclesNodeService(properties, null)
            {
                PrivateKey = privateKey
            };
        }
    }
}
