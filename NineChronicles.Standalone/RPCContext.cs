using System.Collections.Immutable;
using Libplanet;

namespace NineChronicles.Standalone
{
    public class RpcContext
    {
        public ImmutableHashSet<Address> AddressesToSubscribe = ImmutableHashSet<Address>.Empty;
    }
}
