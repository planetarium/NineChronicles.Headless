using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Tx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class NodeStatusType : ObjectGraphType<StandaloneContext>
    {
        private static readonly string _productVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private static readonly string _informationalVersion =
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "Unknown";

        public NodeStatusType()
        {
            Field<NonNullGraphType<BooleanGraphType>>("bootstrapEnded")
                .Description("Whether the current libplanet node has ended bootstrapping.")
                .Resolve(ctx => ctx.Source.BootstrapEnded);
            Field<NonNullGraphType<BooleanGraphType>>("preloadEnded")
                .Description("Whether the current libplanet node has ended preloading.")
                .Resolve(ctx => ctx.Source.PreloadEnded);
            Field<NonNullGraphType<BlockHeaderType>>("tip")
                .Description("Block header of the tip block from the current canonical chain.")
                .Resolve(ctx => ctx.Source.BlockChain?.Tip.Header);
            Field<NonNullGraphType<ListGraphType<BlockHeaderType>>>("topmostBlocks")
                .Argument<int>(
                    "limit",
                    false,
                    "The number of blocks to get.")
                .Argument<int?>(
                    "offset",
                    true,
                    "The number of blocks to skip from tip.",
                    arg => arg.DefaultValue = 0)
                .Argument<Address?>(
                    "miner",
                    true,
                    "List only blocks mined by the given address.  (List everything if omitted.)",
                    arg => arg.DefaultValue = null)
                .Description("The topmost blocks from the current node.")
                .Resolve(ctx =>
                {
                    if (!(ctx.Source.BlockChain is { } chain))
                    {
                        throw new InvalidOperationException($"{nameof(ctx.Source.BlockChain)} is null.");
                    }

                    IEnumerable<Block<NCAction>> blocks =
                        GetTopmostBlocks(chain, ctx.GetArgument<int>("offset"));
                    if (ctx.GetArgument<Address?>("miner") is { } miner)
                    {
                        blocks = blocks.Where(b => b.Miner.Equals(miner));
                    }

                    return blocks
                        .Take(ctx.GetArgument<int>("limit"))
                        .Select(block => block.Header);
                });
            Field<ListGraphType<TxIdType>>("stagedTxIds")
                .Argument<Address?>("address", true, "Target address to query")
                .Description("Ids of staged transactions from the current node.")
                .Resolve(ctx =>
                {
                    if (!(ctx.Source.BlockChain is { } chain))
                    {
                        throw new InvalidOperationException($"{nameof(ctx.Source.BlockChain)} is null.");
                    }

                    if (!ctx.HasArgument("address"))
                    {
                        return chain.GetStagedTransactionIds();
                    }
                    else
                    {
                        Address address = ctx.GetArgument<Address>("address");
                        IImmutableSet<TxId> stagedTransactionIds = chain.GetStagedTransactionIds();

                        return stagedTransactionIds.Where(txId =>
                        chain.GetTransaction(txId).Signer.Equals(address));
                    }
                });
            Field<IntGraphType>("stagedTxIdsCount")
                .Description("The number of ids of staged transactions from the current node.")
                .Resolve(ctx => ctx.Source.BlockChain is { } chain
                    ? chain.GetStagedTransactionIds().Count
                    : throw new InvalidOperationException($"{nameof(ctx.Source.BlockChain)} is null."));
            Field<NonNullGraphType<BlockHeaderType>>("genesis")
                .Description("Block header of the genesis block from the current chain.")
                .Resolve(ctx => ctx.Source.BlockChain?.Genesis.Header);
            Field<NonNullGraphType<BooleanGraphType>>("isMining")
                .Description("Whether the current node is mining.")
                .Resolve(ctx => ctx.Source.IsMining);
            Field<AppProtocolVersionType>("appProtocolVersion")
                .Resolve(ctx => ctx.Source.NineChroniclesNodeService?.Swarm.AppProtocolVersion);

            Field<ListGraphType<AddressType>>("subscriberAddresses")
                .Description("A list of subscribers' address")
                .Resolve(ctx => ctx.Source.AgentAddresses.Keys);

            Field<IntGraphType>("subscriberAddressesCount")
                .Description("The number of a list of subscribers' address")
                .Resolve(ctx => ctx.Source.AgentAddresses.Count);

            Field<StringGraphType>("productVersion")
                .Description("A version of NineChronicles.Headless")
                .Resolve(_ => _productVersion);

            Field<StringGraphType>("informationalVersion")
                .Description(
                    "A informational version (a.k.a. version suffix) of NineChronicles.Headless")
                .Resolve(_ => _informationalVersion);
        }

        private IEnumerable<Block<T>> GetTopmostBlocks<T>(BlockChain<T> blockChain, int offset)
            where T : IAction, new()
        {
            Block<T> block = blockChain.Tip;

            while (offset > 0)
            {
                offset--;
                if (block.PreviousHash is { } prev)
                {
                    block = blockChain[prev];
                }
            }

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
