using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class PublicKeyType : ObjectGraphType<PublicKey>
    {
        public PublicKeyType()
        {
            Field<NonNullGraphType<ByteStringType>>("hex")
                .Description("A representation of public-key with hexadecimal format.")
                .Argument<bool?>("compress", true, "A flag to determine whether to compress public-key.")
                .Resolve(context =>
                {
                    var compress = context.GetArgument<bool>("compress");
                    return context.Source.Format(compress);
                });

            Field<NonNullGraphType<AddressType>>("address")
                .Description("An address derived from the public-key.")
                .Resolve(context => context.Source.ToAddress());
        }
    }
}
