using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Explorer.GraphTypes;
using Block = NineChronicles.Headless.Domain.Model.BlockChain.Block;

namespace NineChronicles.Headless.GraphTypes
{
    public class BlockHeaderType : ObjectGraphType<BlockHeaderType>
    {
        public long Index { get; set; }

        public BlockHash Hash { get; set; }

        public Address? Miner { get; set; }

        public BlockHeaderType()
        {
            Name = "BlockHeader";

            Field<NonNullGraphType<IntGraphType>>(
                name: "index",
                resolve: context => context.Source.Index
            );
            Field<NonNullGraphType<IdGraphType>>(
                name: "id",
                resolve: context => context.Source.Hash.ToString()
            );
            Field<NonNullGraphType<StringGraphType>>(
                name: "hash",
                resolve: context => context.Source.Hash.ToString()
            );
            Field<AddressType>(
                name: "miner",
                resolve: context => context.Source.Miner
            );
        }

        public static BlockHeaderType FromBlock(Block block) =>
            new BlockHeaderType
            {
                Index = block.Index,
                Hash = block.Hash,
                Miner = block.Miner,
            };
    }
}
