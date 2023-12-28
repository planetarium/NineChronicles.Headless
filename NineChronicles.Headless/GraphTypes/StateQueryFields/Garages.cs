using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Model.Garages;
using Nekoyume.Module;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes;

public partial class StateQuery
{
    private void RegisterGarages()
    {
        Field<GaragesType>(
            "garages",
            description: "Get balances and fungible items in garages.\n" +
                         "Use either `currencyEnums` or `currencyTickers` to get balances.",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddr",
                    Description = "Agent address to get balances and fungible items in garages",
                },
                new QueryArgument<ListGraphType<NonNullGraphType<CurrencyEnumType>>>
                {
                    Name = "currencyEnums",
                    Description = "List of currency enums to get balances in garages",
                },
                new QueryArgument<ListGraphType<NonNullGraphType<StringGraphType>>>
                {
                    Name = "currencyTickers",
                    Description = "List of currency tickers to get balances in garages",
                },
                new QueryArgument<ListGraphType<NonNullGraphType<StringGraphType>>>
                {
                    Name = "fungibleItemIds",
                    Description = "List of fungible item IDs to get fungible item in garages",
                }
            ),
            resolve: context =>
            {
                var agentAddr = context.GetArgument<Address>("agentAddr");
                var garageBalanceAddr = Addresses.GetGarageBalanceAddress(agentAddr);
                var currencyEnums = context.GetArgument<CurrencyEnum[]?>("currencyEnums");
                var currencyTickers = context.GetArgument<string[]?>("currencyTickers");
                var garageBalances = new List<FungibleAssetValue>();
                if (currencyEnums is not null)
                {
                    if (currencyTickers is not null)
                    {
                        throw new ExecutionError(
                            "Use either `currencyEnums` or `currencyTickers` to get balances.");
                    }

                    foreach (var currencyEnum in currencyEnums)
                    {
                        if (!context.Source.CurrencyFactory.TryGetCurrency(currencyEnum, out var currency))
                        {
                            throw new ExecutionError($"Invalid currency enum: {currencyEnum}");
                        }

                        var balance = context.Source.WorldState.GetBalance(garageBalanceAddr, currency);
                        garageBalances.Add(balance);
                    }
                }
                else if (currencyTickers is not null)
                {
                    foreach (var currencyTicker in currencyTickers)
                    {
                        if (!context.Source.CurrencyFactory.TryGetCurrency(currencyTicker, out var currency))
                        {
                            throw new ExecutionError($"Invalid currency ticker: {currencyTicker}");
                        }

                        var balance = context.Source.WorldState.GetBalance(garageBalanceAddr, currency);
                        garageBalances.Add(balance);
                    }
                }

                IEnumerable<(string, Address, FungibleItemGarage?)> fungibleItemGarages;
                var fungibleItemIds = context.GetArgument<string[]?>("fungibleItemIds");
                if (fungibleItemIds is null)
                {
                    fungibleItemGarages = Enumerable.Empty<(string, Address, FungibleItemGarage?)>();
                }
                else
                {
                    var fungibleItemGarageAddresses = fungibleItemIds
                        .Select(fungibleItemId => Addresses.GetGarageAddress(
                            agentAddr,
                            HashDigest<SHA256>.FromString(fungibleItemId)))
                        .ToArray();
                    fungibleItemGarages = fungibleItemGarageAddresses
                        .Select(address => context.Source.WorldState.GetLegacyState(address))
                        .Select((value, i) => value is null or Null
                            ? (fungibleItemIds[i], fungibleItemGarageAddresses[i], null)
                            : (fungibleItemIds[i], fungibleItemGarageAddresses[i], new FungibleItemGarage(value)));
                }

                return new GaragesType.Value(
                    agentAddr,
                    garageBalanceAddr,
                    garageBalances,
                    fungibleItemGarages);
            }
        );
    }
}
