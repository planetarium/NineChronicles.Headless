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
            Field<NonNullGraphType<ByteStringType>>(
                name: "hex",
                description: "A representation of private-key with hexadecimal format.",
                resolve: context => context.Source.ToByteArray());

            Field<NonNullGraphType<PublicKeyType>>(
                name: nameof(PrivateKey.PublicKey),
                description: "A public-key derived from the private-key.");
        }
    }
}
