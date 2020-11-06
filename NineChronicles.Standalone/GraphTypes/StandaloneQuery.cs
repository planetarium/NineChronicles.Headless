using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Nekoyume.Action;
using Nekoyume.Model.State;

namespace NineChronicles.Standalone.GraphTypes
{
    public class StandaloneQuery : ObjectGraphType
    {
        public StandaloneQuery(StandaloneContext standaloneContext)
        {
            Field<ByteStringType>(
                name: "state",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "The address of state to fetch from the chain." },
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "The hash of the block used to fetch state from chain." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var address = context.GetArgument<Address>("address");
                    var blockHashByteArray = context.GetArgument<byte[]>("hash");
                    var blockHash = blockHashByteArray is null
                        ? blockChain.Tip.Hash
                        : new HashDigest<SHA256>(blockHashByteArray);

                    var state = blockChain.GetState(address, blockHash);

                    return new Codec().Encode(state);
                }
            );

            Field<KeyStoreType>(
                name: "keyStore",
                resolve: context => standaloneContext.KeyStore
            );

            Field<NonNullGraphType<NodeStatusType>>(
                name: "nodeStatus",
                resolve: context => new NodeStatusType
                {
                    BootstrapEnded = standaloneContext.BootstrapEnded,
                    PreloadEnded = standaloneContext.PreloadEnded,
                    BlockChain = standaloneContext.BlockChain,
                    Store = standaloneContext.Store,
                }
            );

            Field<NonNullGraphType<ValidationQuery>>(
                name: "validation",
                description: "The validation method provider for Libplanet types.",
                resolve: context => new ValidationQuery(standaloneContext));

            Field<NonNullGraphType<ActivationStatusQuery>>(
                name: "activationStatus",
                description: "Check if the provided address is activated.",
                resolve: context => new ActivationStatusQuery(standaloneContext));

            Field<NonNullGraphType<PeerChainStateQuery>>(
                name: "peerChainState",
                description: "Get the peer's block chain state",
                resolve: context => new PeerChainStateQuery(standaloneContext));

            Field<NonNullGraphType<StringGraphType>>(
                name: "goldBalance",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "Target address to query" },
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "Offset block hash for query." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address address = context.GetArgument<Address>("address");
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    var blockHash = blockHashByteArray is null
                        ? blockChain.Tip.Hash
                        : new HashDigest<SHA256>(blockHashByteArray);
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)blockChain.GetState(GoldCurrencyState.Address)
                    ).Currency;

                    return blockChain.GetBalance(
                        address,
                        currency,
                        blockHash
                    ).GetQuantityString();
                }
            );

            Field<NonNullGraphType<LongGraphType>>(
                name: "nextTxNonce",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "Target address to query" }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address address = context.GetArgument<Address>("address");
                    return blockChain.GetNextTxNonce(address);
                }
            );
        }
    }
}
