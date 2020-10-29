using System.Security.Cryptography;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;

namespace NineChronicles.Standalone.GraphTypes
{
    public class BlockHeaderType : ObjectGraphType<BlockHeaderType>
    {
        public long Index { get; set; }

        public HashDigest<SHA256> Hash { get; set; }

        public BlockHeaderType()
        {
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
        }

        public static BlockHeaderType FromBlock<T>(Block<T> block)
            where T : IAction, new() =>
            new BlockHeaderType
            {
                Index = block.Index,
                Hash = block.Hash,
            };
    }
}
