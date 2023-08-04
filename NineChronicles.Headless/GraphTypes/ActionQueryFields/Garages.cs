using System.Collections.Generic;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Action.Garages;
using NineChronicles.Headless.GraphTypes.Input;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class ActionQuery
    {
        private void RegisterGarages()
        {
            Field<NonNullGraphType<ByteStringType>>(
                "loadIntoMyGarages",
                arguments: new QueryArguments(
                    new QueryArgument<ListGraphType<NonNullGraphType<BalanceInputType>>>
                    {
                        Name = "fungibleAssetValues",
                        Description = "Array of balance address and currency ticker and quantity.",
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "inventoryAddr",
                        Description = "Inventory Address",
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<FungibleIdAndCountInputType>>>
                    {
                        Name = "fungibleIdAndCounts",
                        Description = "Array of fungible ID and count",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Name = "memo",
                        Description = "Memo",
                    }
                ),
                resolve: context =>
                {
                    var balanceInputList = context.GetArgument<IEnumerable<(
                        Address balanceAddr,
                        (string currencyTicker, string value))>?>("fungibleAssetValues");
                    List<(Address address, FungibleAssetValue fungibleAssetValue)>? fungibleAssetValues = null;
                    if (balanceInputList is not null)
                    {
                        fungibleAssetValues = new List<(Address address, FungibleAssetValue fungibleAssetValue)>();
                        foreach (var (balanceAddr, (currencyTicker, value)) in balanceInputList)
                        {
                            if (StandaloneContext.FungibleAssetValueFactory!.TryGetFungibleAssetValue(currencyTicker, value, out var fav))
                            {
                                fungibleAssetValues.Add((balanceAddr, fav));
                            }
                            else
                            {
                                throw new ExecutionError($"Invalid currency ticker: {currencyTicker}");
                            }
                        }
                    }

                    var inventoryAddr = context.GetArgument<Address?>("inventoryAddr");
                    var fungibleIdAndCounts = context.GetArgument<IEnumerable<(
                        HashDigest<SHA256> fungibleId,
                        int count)>?>("fungibleIdAndCounts");
                    var memo = context.GetArgument<string?>("memo");

                    ActionBase action = new LoadIntoMyGarages(
                        fungibleAssetValues,
                        inventoryAddr,
                        fungibleIdAndCounts,
                        memo);
                    return Encode(context, action);
                }
            );

            Field<NonNullGraphType<ByteStringType>>(
                "deliverToOthersGarages",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "recipientAgentAddr",
                        Description = "Recipient agent address",
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<SimplifyFungibleAssetValueInputType>>>
                    {
                        Name = "fungibleAssetValues",
                        Description = "Array of currency ticket and quantity to deliver.",
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<FungibleIdAndCountInputType>>>
                    {
                        Name = "fungibleIdAndCounts",
                        Description = "Array of Fungible ID and count to deliver.",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Name = "memo",
                        Description = "Memo",
                    }
                ),
                resolve: context =>
                {
                    var recipientAgentAddr = context.GetArgument<Address>("recipientAgentAddr");
                    var fungibleAssetValueInputList = context.GetArgument<IEnumerable<(
                        string currencyTicker,
                        string value)>?>("fungibleAssetValues");
                    List<FungibleAssetValue>? fungibleAssetValues = null;
                    if (fungibleAssetValueInputList is not null)
                    {
                        fungibleAssetValues = new List<FungibleAssetValue>();
                        foreach (var (currencyTicker, value) in fungibleAssetValueInputList)
                        {
                            if (StandaloneContext.FungibleAssetValueFactory!.TryGetFungibleAssetValue(currencyTicker, value, out var fav))
                            {
                                fungibleAssetValues.Add(fav);
                            }
                            else
                            {
                                throw new ExecutionError($"Invalid currency ticker: {currencyTicker}");
                            }
                        }
                    }

                    var fungibleIdAndCounts = context.GetArgument<IEnumerable<(
                        HashDigest<SHA256> fungibleId,
                        int count)>?>("fungibleIdAndCounts");
                    var memo = context.GetArgument<string?>("memo");

                    ActionBase action = new DeliverToOthersGarages(
                        recipientAgentAddr,
                        fungibleAssetValues,
                        fungibleIdAndCounts,
                        memo);
                    return Encode(context, action);
                }
            );

            Field<NonNullGraphType<ByteStringType>>(
                "unloadFromMyGarages",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "recipientAvatarAddr",
                        Description = "Recipient avatar address",
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<BalanceInputType>>>
                    {
                        Name = "fungibleAssetValues",
                        Description = "Array of balance address and currency ticker and quantity to send.",
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<FungibleIdAndCountInputType>>>
                    {
                        Name = "fungibleIdAndCounts",
                        Description = "Array of fungible ID and count to send.",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Name = "memo",
                        Description = "Memo",
                    }
                ),
                resolve: context =>
                {
                    var recipientAvatarAddr = context.GetArgument<Address>("recipientAvatarAddr");
                    var balanceInputList = context.GetArgument<IEnumerable<(
                        Address balanceAddr,
                        (string currencyTicker, string value))>?>("fungibleAssetValues");
                    List<(Address address, FungibleAssetValue fungibleAssetValue)>? fungibleAssetValues = null;
                    if (balanceInputList is not null)
                    {
                        fungibleAssetValues = new List<(Address address, FungibleAssetValue fungibleAssetValue)>();
                        foreach (var (addr, (currencyTicker, value)) in balanceInputList)
                        {
                            if (StandaloneContext.FungibleAssetValueFactory!.TryGetFungibleAssetValue(currencyTicker, value, out var fav))
                            {
                                fungibleAssetValues.Add((addr, fav));
                            }
                            else
                            {
                                throw new ExecutionError($"Invalid currency ticker: {currencyTicker}");
                            }
                        }
                    }

                    var fungibleIdAndCounts = context.GetArgument<IEnumerable<(
                        HashDigest<SHA256> fungibleId,
                        int count)>?>("fungibleIdAndCounts");
                    var memo = context.GetArgument<string?>("memo");

                    ActionBase action = new UnloadFromMyGarages(
                        recipientAvatarAddr,
                        fungibleAssetValues,
                        fungibleIdAndCounts,
                        memo);
                    return Encode(context, action);
                }
            );
        }
    }
}
