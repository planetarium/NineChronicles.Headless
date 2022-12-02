using GraphQL.Types;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class BlockHeaderType : ObjectGraphType<BlockHeader>
    {
        public BlockHeaderType()
        {
            Name = "BlockHeader";

            Field<NonNullGraphType<IntGraphType>>("index")
                .Resolve(context => context.Source.Index);
            Field<NonNullGraphType<IdGraphType>>("id")
                .Resolve(context => context.Source.Hash.ToString());
            Field<NonNullGraphType<StringGraphType>>("hash")
                .Resolve(context => context.Source.Hash.ToString());
            Field<AddressType>("miner")
                .Resolve(context => context.Source.Miner);
        }
    }
}
