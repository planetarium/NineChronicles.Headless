using System.Linq;
using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Microsoft.Extensions.Configuration;
using Libplanet.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>; 

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneQuery : ObjectGraphType
    {
        public StandaloneQuery(StandaloneContext standaloneContext, IConfiguration configuration)
        {
            bool useSecretToken = configuration[GraphQLService.SecretTokenKey] is { };

            Field<NonNullGraphType<StateQuery<NCAction>>>(name: "stateQuery", resolve: _ => standaloneContext.BlockChain);
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
            ).AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<NodeStatusType>>(
                name: "nodeStatus",
                resolve: context => new NodeStatusType
                {
                    BootstrapEnded = standaloneContext.BootstrapEnded,
                    PreloadEnded = standaloneContext.PreloadEnded,
                    IsMining = standaloneContext.IsMining,
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
                    resolve: context => new ActivationStatusQuery(standaloneContext))
                .AuthorizeWithLocalPolicyIf(useSecretToken);

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

            Field<TransactionType<NCAction>>(
                name: "getTx",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TxIdType>>
                        {Name = "txId", Description = "transaction id."}
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var txId = context.GetArgument<TxId>("txId");
                    return blockChain.GetTransaction(txId);
                }
            );

            Field<NonNullGraphType<ListGraphType<ShopItemType>>>(
                name: "products",
                arguments: new QueryArguments(
                    new QueryArgument<IntGraphType>
                    {
                        Name = "id",
                        Description = "Filter for item id."
                    },
                    new QueryArgument<ItemSubTypeEnumType>
                    {
                        Name = "itemSubType",
                        Description = "Filter for ItemSubType. see from https://github.com/planetarium/lib9c/blob/main/Lib9c/Model/Item/ItemType.cs#L13"
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "maximumPrice",
                        Description = "Filter for item maximum price."
                    }),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var shop = new ShopState((Dictionary) blockChain.GetState(Addresses.Shop));
                    var products = shop.Products.Values;
                    var id = context.GetArgument<int?>("id");
                    if (!(id is null))
                    {
                        products = products
                            .Where(si => si.ItemUsable?.Id == id || si.Costume?.Id == id);
                    }
                    var subType = context.GetArgument<ItemSubType?>("itemSubType");
                    if (!(subType is null))
                    {
                        products = products
                            .Where(si => si.ItemUsable?.ItemSubType == subType || si.Costume?.ItemSubType == subType);
                    }
                    var price = context.GetArgument<int?>("maximumPrice");
                    if (!(price is null))
                    {
                        var currency = new GoldCurrencyState(
                            (Dictionary) blockChain.GetState(GoldCurrencyState.Address)
                        ).Currency;
                        products = products
                            .Where(si => si.Price <= price * currency);
                    }
                    return products.ToList();
                }
            );
        }
    }
}
