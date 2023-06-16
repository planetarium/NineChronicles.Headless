using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Action.Garages;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.Input;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class ActionQuery
    {
        private FungibleAssetValue getFungibleAssetValue(
            CurrencyEnum curr, BigInteger majorUnit, BigInteger minorUnit)
        {
            Currency currency;
            switch (curr)
            {
                case CurrencyEnum.NCG:
                    currency = new GoldCurrencyState(
                        (Dictionary)standaloneContext.BlockChain!.GetState(GoldCurrencyState.Address)
                    ).Currency;
                    break;
                case CurrencyEnum.CRYSTAL:
                    currency = CrystalCalculator.CRYSTAL;
                    break;
                default:
                    throw new ExecutionError($"Unsupported Currency type {curr}");
            }

            return new FungibleAssetValue(currency, majorUnit, minorUnit);
        }

        private void RegisterGarages()
        {
            Field<NonNullGraphType<ByteStringType>>(
                "loadIntoMyGarages",
                arguments: new QueryArguments(
                    new QueryArgument<ListGraphType<
                        NonNullGraphType<GarageAddressAndFungibleAssetValueInputType>>>
                    {
                        Name = "addressAndFungibleAssetValues",
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
                    // This transforms to (Address, FungibleAssetValue) and goes to....
                    var addressAndFavList = context
                        .GetArgument<IEnumerable<(Address balanceAddr,
                            (CurrencyEnum Currency, BigInteger majorUnit, BigInteger minorUnit)
                            )>>("addressAndFungibleAssetValues");
                    // Here. and This is the input type of action.
                    var fungibleAssetValues = new List<(Address balanceAddr, FungibleAssetValue value)>();

                    foreach (var (addr, (curr, majorUnit, minorUnit)) in addressAndFavList)
                    {
                        var fav = getFungibleAssetValue(curr, majorUnit, minorUnit);
                        fungibleAssetValues.Add((addr, fav));
                    }

                    var inventoryAddr = context.GetArgument<Address?>("inventoryAddr");
                    var fungibleIdAndCounts =
                        context.GetArgument<IEnumerable<(HashDigest<SHA256> fungibleId, int count)>?>(
                            "fungibleIdAndCounts");
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
                    new QueryArgument<ListGraphType<NonNullGraphType<GarageFungibleAssetValueInputType>>>
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
                    var fungibleAssetValueList = context.GetArgument<
                        IEnumerable<(CurrencyEnum CurrencyEnum, BigInteger majorUnit, BigInteger minorUnit)>
                    >("fungibleAssetValues");
                    var fungibleAssetValues = new List<FungibleAssetValue>();
                    foreach (var (curr, majorUnit, minorUnit) in fungibleAssetValueList)
                    {
                        fungibleAssetValues.Add(getFungibleAssetValue(curr, majorUnit, minorUnit));
                    }

                    var fungibleIdAndCounts =
                        context.GetArgument<IEnumerable<(HashDigest<SHA256> fungibleId, int count)>?>(
                            "fungibleIdAndCounts");
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
                    new QueryArgument<ListGraphType<NonNullGraphType<GarageAddressAndFungibleAssetValueInputType>>>
                    {
                        Name = "addressAndFungibleAssetValues",
                        Description = "Array of balance address and currency ticker and quantity to send.",
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "inventoryAddr",
                        Description = "Inventory address to receive items.",
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
                    var addressAndFavList = context
                        .GetArgument<IEnumerable<(Address balanceAddr,
                            (CurrencyEnum Currency, BigInteger majorUnit, BigInteger minorUnit)
                            )>>("addressAndFungibleAssetValues");
                    var fungibleAssetValues = new List<(Address balanceAddr, FungibleAssetValue value)>();
                    foreach (var (addr, (curr, majorUnit, minorUnit)) in addressAndFavList)
                    {
                        var fav = getFungibleAssetValue(curr, majorUnit, minorUnit);
                        fungibleAssetValues.Add((addr, fav));
                    }

                    var inventoryAddr = context.GetArgument<Address?>("inventoryAddr");
                    var fungibleIdAndCounts =
                        context.GetArgument<IEnumerable<(HashDigest<SHA256> fungibleId, int count)>?>(
                            "fungibleIdAndCounts");
                    var memo = context.GetArgument<string?>("memo");

                    ActionBase action = new UnloadFromMyGarages(
                        fungibleAssetValues,
                        inventoryAddr,
                        fungibleIdAndCounts,
                        memo);
                    return Encode(context, action);
                }
            );
        }
    }
}
