using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Standalone.GraphTypes
{
    public class NodeStatusType : ObjectGraphType<NodeStatusType>
    {
        public bool BootstrapEnded { get; set; }

        public bool PreloadEnded { get; set; }

        public BlockChain<NCAction> BlockChain { get; set; }

        public NodeStatusType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(name: "bootstrapEnded",
                resolve: context => context.Source.BootstrapEnded);
            Field<NonNullGraphType<BooleanGraphType>>(name: "preloadEnded",
                resolve: context => context.Source.PreloadEnded);
            Field<NonNullGraphType<BlockHeaderType>>(name: "tip",
                resolve: context => BlockHeaderType.FromBlock(context.Source.BlockChain.Tip));
            Field<NonNullGraphType<ListGraphType<BlockHeaderType>>>(
                name: "topmostBlocks",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "limit",
                        Description = "The number of blocks to get."
                    }
                ),
                description: "The topmost blocks from the current node.",
                resolve: context => GetTopmostBlocks(context.Source.BlockChain)
                    .Take(context.GetArgument<int>("limit"))
                    .Select(BlockHeaderType.FromBlock)
            );
            Field<NonNullGraphType<BlockHeaderType>>(name: "genesis",
                resolve: context => BlockHeaderType.FromBlock(context.Source.BlockChain.Genesis));
        }

        private IEnumerable<Block<T>> GetTopmostBlocks<T>(BlockChain<T> blockChain)
            where T : IAction, new()
        {
            Block<T> block = blockChain.Tip;

            while (true)
            {
                yield return block;
                if (block.PreviousHash is HashDigest<SHA256> prev)
                {
                    block = blockChain[prev];
                }
                else
                {
                    break;
                }
            }
        }
    }
}
