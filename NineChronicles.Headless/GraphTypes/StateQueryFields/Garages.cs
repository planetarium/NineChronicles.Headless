using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Model.Garages;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes;

public partial class StateQuery
{
    private void RegisterGarages()
    {
        Field<GaragesType>(
            "garages",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddr",
                    Description = "Agent address to get balances and fungible items in garages",
                },
                new QueryArgument<ListGraphType<NonNullGraphType<SimplifyCurrencyInputType>>>
                {
                    Name = "currencyTickers",
                    Description = "List of currency tickers to get balances in garages",
                },
                new QueryArgument<ListGraphType<NonNullGraphType<FungibleItemIdInputType>>>
                {
                    Name = "fungibleItemIds",
                    Description = "List of fungible item IDs to get fungible item in garages",
                }
            ),
            resolve: context =>
            {
                var agentAddr = context.GetArgument<Address>("agentAddr");
                var garageBalanceAddr = Addresses.GetGarageBalanceAddress(agentAddr);
                var currencyTickers = context.GetArgument<string[]>("currencyTickers");
                var fungibleAssetValues = new List<FungibleAssetValue>();
                foreach (var currencyTicker in currencyTickers)
                {
                    if (!context.Source.CurrencyFactory.TryGetCurrency(currencyTicker, out var currency))
                    {
                        throw new ExecutionError($"Invalid currency ticker: {currencyTicker}");
                    }

                    var balance = context.Source.GetBalance(garageBalanceAddr, currency);
                    fungibleAssetValues.Add(balance);
                }

                var materialItemSheetAddr = Addresses.GetSheetAddress<MaterialItemSheet>();
                var materialItemSheetValue = context.Source.GetState(materialItemSheetAddr);
                if (materialItemSheetValue is null)
                {
                    throw new ExecutionError($"{nameof(MaterialItemSheet)} not found: {materialItemSheetAddr}");
                }

                var materialItemSheet = new MaterialItemSheet();
                materialItemSheet.Set((Text)materialItemSheetValue);
                var fungibleItemIdTuples =
                    context.GetArgument<(string? fungibleItemId, int? itemSheetId)[]>("fungibleItemIds");
                var fungibleItemGarageAddresses = fungibleItemIdTuples
                    .Select(tuple =>
                    {
                        var (fungibleItemId, itemSheetId) = tuple;
                        if (fungibleItemId is not null)
                        {
                            return Addresses.GetGarageAddress(
                                agentAddr,
                                HashDigest<SHA256>.FromString(fungibleItemId));
                        }

                        if (itemSheetId is not null)
                        {
                            var row = materialItemSheet.OrderedList!.FirstOrDefault(r => r.Id == itemSheetId);
                            if (row is null)
                            {
                                throw new ExecutionError($"Invalid item sheet id: {itemSheetId}");
                            }

                            return Addresses.GetGarageAddress(agentAddr, row.ItemId);
                        }

                        throw new ExecutionError(
                            $"Invalid argument: {nameof(fungibleItemId)} or {nameof(itemSheetId)} must be specified.");
                    })
                    .ToArray();
                var fungibleItemGarages = context.Source.GetStates(fungibleItemGarageAddresses)
                    .Select((value, i) => (new FungibleItemGarage(value), fungibleItemGarageAddresses[i]));
                return new GaragesType.Value(
                    agentAddr,
                    garageBalanceAddr,
                    fungibleAssetValues,
                    fungibleItemGarages);
            }
        );
    }
}
