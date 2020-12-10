using GraphQL.Types;
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
