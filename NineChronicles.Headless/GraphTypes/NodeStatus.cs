using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Tx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using NineChronicles.Headless.Repositories.BlockChain;
using Block = NineChronicles.Headless.Domain.Model.BlockChain.Block;

namespace NineChronicles.Headless.GraphTypes
{
    public class NodeStatusType : ObjectGraphType<NodeStatusType.NodeStatus>
    {
#pragma warning disable SA1313
        public record NodeStatus(bool BootstrapEnded, bool PreloadEnded, bool IsMining);
#pragma warning restore SA1313

        private static readonly string _productVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private static readonly string _informationalVersion =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "Unknown";

        public NodeStatusType(StandaloneContext context, IBlockChainRepository blockChainRepository)
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "bootstrapEnded",
                description: "Whether the current libplanet node has ended bootstrapping.",
                resolve: _ => context.BootstrapEnded
            );
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "preloadEnded",
                description: "Whether the current libplanet node has ended preloading.",
                resolve: _ => context.PreloadEnded
            );
            Field<NonNullGraphType<BlockHeaderType>>(
                name: "tip",
                description: "Block header of the tip block from the current canonical chain.",
                resolve: _ => BlockHeaderType.FromBlock(blockChainRepository.GetTip())
            );
            Field<NonNullGraphType<ListGraphType<BlockHeaderType>>>(
                name: "topmostBlocks",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "limit",
                        Description = "The number of blocks to get."
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "offset",
                        Description = "The number of blocks to skip from tip.",
                        DefaultValue = 0,
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
                resolve: fieldContext =>
                {
                    IEnumerable<Block> blocks =
                        blockChainRepository.IterateBlocksDescending(fieldContext.GetArgument<int>("offset"));
                    if (fieldContext.GetArgument<Address?>("miner") is { } miner)
                    {
                        blocks = blocks.Where(b => b.Miner.Equals(miner));
                    }

                    return blocks
                        .Take(fieldContext.GetArgument<int>("limit"))
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
                resolve: fieldContext =>
                {
                    if (context.BlockChain is null)
                    {
                        throw new InvalidOperationException($"{nameof(context.BlockChain)} is null.");
                    }

                    if (!fieldContext.HasArgument("address"))
                    {
                        return context.BlockChain.GetStagedTransactionIds();
                    }
                    else
                    {
                        Address address = fieldContext.GetArgument<Address>("address");
                        IImmutableSet<TxId> stagedTransactionIds = context.BlockChain.GetStagedTransactionIds();

                        return stagedTransactionIds.Where(txId =>
                        context.BlockChain.GetTransaction(txId).Signer.Equals(address));
                    }
                }
            );
            Field<IntGraphType>(
                name: "stagedTxIdsCount",
                description: "The number of ids of staged transactions from the current node.",
                resolve: fieldContext =>
                {
                    if (context.BlockChain is null)
                    {
                        throw new InvalidOperationException($"{nameof(context.BlockChain)} is null.");
                    }

                    return context.BlockChain.GetStagedTransactionIds().Count;
                }
            );
            Field<NonNullGraphType<BlockHeaderType>>(
                name: "genesis",
                description: "Block header of the genesis block from the current chain.",
                resolve: fieldContext =>
                    BlockHeaderType.FromBlock(blockChainRepository.GetBlock(0))
            );
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "isMining",
                description: "Whether the current node is mining.",
                resolve: _ => context.IsMining
            );
            Field<AppProtocolVersionType>(
                "appProtocolVersion",
                resolve: _ => context.NineChroniclesNodeService?.Swarm.AppProtocolVersion);

            Field<ListGraphType<AddressType>>(
                name: "subscriberAddresses",
                description: "A list of subscribers' address",
                resolve: _ => context.AgentAddresses.Keys
            );

            Field<IntGraphType>(
                name: "subscriberAddressesCount",
                description: "The number of a list of subscribers' address",
                resolve: _ => context.AgentAddresses.Count
            );

            Field<StringGraphType>(
                name: "productVersion",
                description: "A version of NineChronicles.Headless",
                resolve: _ => _productVersion
            );

            Field<StringGraphType>(
                name: "informationalVersion",
                description: "A informational version (a.k.a. version suffix) of NineChronicles.Headless",
                resolve: _ => _informationalVersion
            );
        }
    }
}
