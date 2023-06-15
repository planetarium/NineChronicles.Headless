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
using NineChronicles.Headless.GraphTypes.ActionArgs.Garages;
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
                        Description = "Array of fungible ID and count"
                    }
                ),
                resolve:
                context =>
                {
                    // This transforms to (Address, FungibleAssetValue) and goes to....
                    var addressAndFavList = context
                        .GetArgument<IEnumerable<(Address balanceAddr,
                            (CurrencyEnum Currency, BigInteger majorUnit, BigInteger minorUnit)
                            )>>("addressAndFungibleAssetValues");
                    // Here. and This is the input type of action.
                    var fungibleAssetValues = new List<(Address balanceAddr, FungibleAssetValue value)>();

                    foreach (var (addr, (curr, majorUnit, minorUnit))
                             in addressAndFavList)
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

                        FungibleAssetValue fav = new FungibleAssetValue(currency, majorUnit, minorUnit);
                        fungibleAssetValues.Add((addr, fav));
                    }

                    var inventoryAddr = context.GetArgument<Address?>("inventoryAddr");
                    var fungibleIdAndCounts =
                        context.GetArgument<IEnumerable<(HashDigest<SHA256> fungibleId, int count)>?>(
                            "fungibleIdAndCounts");

                    ActionBase action = new LoadIntoMyGarages(
                        fungibleAssetValues,
                        inventoryAddr,
                        fungibleIdAndCounts);
                    return Encode(context, action);
                }
            );

            Field<NonNullGraphType<ByteStringType>>(
                "deliverToOthersGarages",
                arguments: new QueryArguments(
                    new QueryArgument<DeliverToOthersGaragesArgsInputType>
                    {
                        Name = "args",
                        Description = "The arguments of the \"DeliverToOthersGarages\" action constructor.",
                    }
                ),
                resolve: context =>
                {
                    var args = context.GetArgument<(
                        Address recipientAgentAddr,
                        IEnumerable<FungibleAssetValue>? fungibleAssetValues,
                        IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts
                        )>("args");
                    ActionBase action = new DeliverToOthersGarages(
                        args.recipientAgentAddr,
                        args.fungibleAssetValues,
                        args.fungibleIdAndCounts);
                    return Encode(context, action);
                }
            );

            Field<NonNullGraphType<ByteStringType>>(
                "unloadFromMyGarages",
                arguments: new QueryArguments(
                    new QueryArgument<UnloadFromMyGaragesArgsInputType>
                    {
                        Name = "args",
                        Description = "The arguments of the \"UnloadFromMyGarages\" action constructor.",
                    }
                ),
                resolve: context =>
                {
                    var args = context.GetArgument<(
                        IEnumerable<(Address balanceAddr, FungibleAssetValue value)>? fungibleAssetValues,
                        Address? inventoryAddr,
                        IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts
                        )>("args");
                    ActionBase action = new UnloadFromMyGarages(
                        args.fungibleAssetValues,
                        args.inventoryAddr,
                        args.fungibleIdAndCounts);
                    return Encode(context, action);
                }
            );
        }
    }
}
