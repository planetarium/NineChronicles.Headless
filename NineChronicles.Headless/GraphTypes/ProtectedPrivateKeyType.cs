using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.KeyStore;

namespace NineChronicles.Headless.GraphTypes
{
    public class ProtectedPrivateKeyType : ObjectGraphType<ProtectedPrivateKey>
    {
        public ProtectedPrivateKeyType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(ProtectedPrivateKey.Address));
        }
    }
}
