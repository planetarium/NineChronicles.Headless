using GraphQL.Types;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class TipChangeEventType : ObjectGraphType<(long Index, BlockHash Hash)>
    {
        public TipChangeEventType()
        {
            Field<NonNullGraphType<LongGraphType>>("index", resolve: context => context.Source.Index);
            Field<NonNullGraphType<ByteStringType>>("hash", resolve: context => context.Source.Hash.ToByteArray());
        }
    }
}
