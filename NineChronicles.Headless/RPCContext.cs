using System.Collections.Immutable;
using Libplanet;

namespace NineChronicles.Headless
{
    public class RpcContext
    {
        public ImmutableHashSet<Address> AddressesToSubscribe = ImmutableHashSet<Address>.Empty;
    }
}
