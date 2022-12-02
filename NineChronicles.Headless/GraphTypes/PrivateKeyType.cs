using GraphQL.Types;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class PrivateKeyType : ObjectGraphType<PrivateKey>
    {
        public PrivateKeyType()
        {
            Field<NonNullGraphType<ByteStringType>>("hex")
                .Description("A representation of private-key with hexadecimal format.")
                .Resolve(context => context.Source.ToByteArray());

            Field<NonNullGraphType<PublicKeyType>>(nameof(PrivateKey.PublicKey))
                .Description("A public-key derived from the private-key.");
        }
    }
}
