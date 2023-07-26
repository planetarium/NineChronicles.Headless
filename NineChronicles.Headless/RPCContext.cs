using System.Collections.Immutable;
using Libplanet.Crypto;

namespace NineChronicles.Headless
{
    public class RpcContext
    {
        public ImmutableHashSet<Address> AddressesToSubscribe = ImmutableHashSet<Address>.Empty;
        public bool RpcRemoteSever;
    }
}
