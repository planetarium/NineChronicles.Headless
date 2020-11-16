using System.Collections.Immutable;
using Libplanet;

namespace NineChronicles.Standalone
{
    public class RpcContext
    {
        public ImmutableList<Address> AddressesToSubscribe = ImmutableList<Address>.Empty;
    }
}
