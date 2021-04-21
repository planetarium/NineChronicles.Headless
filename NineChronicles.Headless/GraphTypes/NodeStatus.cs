using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Store;
using Libplanet.Tx;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class NodeStatusType : ObjectGraphType<NodeStatusType>
    {
        public bool BootstrapEnded { get; set; }

        public bool PreloadEnded { get; set; }
        
        public bool IsMining { get; set; }

        public BlockChain<NCAction>? BlockChain { get; set; }

        public IStore? Store { get; set; }

        public NodeStatusType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "bootstrapEnded",
                description: "Whether the current libplanet node has ended bootstrapping.",
                resolve: context => context.Source.BootstrapEnded
            );
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "preloadEnded",
                description: "Whether the current libplanet node has ended preloading.",
                resolve: context => context.Source.PreloadEnded
            );
            Field<NonNullGraphType<BlockHeaderType>>(
                name: "tip",
                description: "Block header of the tip block from the current canonical chain.",
                resolve: context => context.Source.BlockChain is { } blockChain
                    ? BlockHeaderType.FromBlock(blockChain.Tip)
                    : null
            );
            Field<NonNullGraphType<ListGraphType<BlockHeaderType>>>(
                name: "topmostBlocks",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "limit",
                        Description = "The number of blocks to get."
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "miner",
                        Description = "List only blocks mined by the given address.  " +
                            "(List everything if omitted.)",
                        DefaultValue = null,
                    }
                ),
                description: "The topmost blocks from the current node.",
                resolve: context =>
                {
                    if (context.Source.BlockChain is null)
                    {
                        throw new InvalidOperationException($"{nameof(context.Source.BlockChain)} is null.");
                    }

                    IEnumerable<Block<NCAction>> blocks =
                        GetTopmostBlocks(context.Source.BlockChain);
                    if (context.GetArgument<Address?>("miner") is { } miner)
                    {
                        blocks = blocks.Where(b => b.Miner.Equals(miner));
                    }

                    return blocks
                        .Take(context.GetArgument<int>("limit"))
                        .Select(BlockHeaderType.FromBlock);
                });
            Field<ListGraphType<TxIdType>>(
                name: "stagedTxIds",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "address",
                        Description = "Target address to query"
                    }
                ),
                description: "Ids of staged transactions from the current node.",
                resolve: context =>
                {
                    if (context.Source?.BlockChain is null)
                    {
                        throw new InvalidOperationException($"{nameof(context.Source.BlockChain)} is null.");
                    }

                    if (!context.HasArgument("address"))
                    {
                        return context.Source.BlockChain.GetStagedTransactionIds();
                    }
                    else
                    {
                        Address address = context.GetArgument<Address>("address");
                        IImmutableSet<TxId> stagedTransactionIds = context.Source.BlockChain.GetStagedTransactionIds();

                        return stagedTransactionIds.Where(txId =>
                        context.Source.BlockChain.GetTransaction(txId).Signer.Equals(address));
                    }
                }
            );
            Field<NonNullGraphType<BlockHeaderType>>(
                name: "genesis",
                description: "Block header of the genesis block from the current chain.",
                resolve: context =>
                    context.Source.BlockChain is { } blockChain
                        ? BlockHeaderType.FromBlock(blockChain.Genesis)
                        : null
            );
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "isMining",
                description: "Whether the current node is mining.",
                resolve: context => context.Source.IsMining
            );
        }

        private IEnumerable<Block<T>> GetTopmostBlocks<T>(BlockChain<T> blockChain)
            where T : IAction, new()
        {
            Block<T> block = blockChain.Tip;

            while (true)
            {
                yield return block;
                if (block.PreviousHash is { } prev)
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
