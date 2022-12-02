using Bencodex;
using Libplanet.Net;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public sealed class AppProtocolVersionType : ObjectGraphType<AppProtocolVersion>
    {
        private static Codec _codec = new Codec();

        public AppProtocolVersionType()
        {
            Field<NonNullGraphType<IntGraphType>>("version")
                .Resolve(context => context.Source.Version);
            Field<NonNullGraphType<AddressType>>("signer")
                .Resolve(context => context.Source.Signer);
            Field<NonNullGraphType<ByteStringType>>("signature")
                .Resolve(context => context.Source.Signature.ToBuilder().ToArray());
            Field<ByteStringType>("extra")
                .Resolve(context => _codec.Encode(context.Source.Extra));
        }
    }
}
