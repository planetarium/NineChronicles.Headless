using System.Collections.Generic;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Garages;
using NineChronicles.Headless.GraphTypes.States.Models.Garage;

namespace NineChronicles.Headless.GraphTypes.States;

public class GaragesType : ObjectGraphType<GaragesType.Value>
{
    public struct Value
    {
        public readonly Address AgentAddr;
        public readonly Address GarageBalancesAddr;
        public readonly IEnumerable<FungibleAssetValue> GarageBalances;

        public readonly IEnumerable<(string fungibleItemId, Address addr, FungibleItemGarage? fungibleItemGarage)>
            FungibleItemGarages;

        public Value(
            Address agentAddr,
            Address garageBalancesAddr,
            IEnumerable<FungibleAssetValue> garageBalances,
            IEnumerable<(string fungibleItemId, Address addr, FungibleItemGarage? fungibleItemGarage)>
                fungibleItemGarages)
        {
            AgentAddr = agentAddr;
            GarageBalancesAddr = garageBalancesAddr;
            GarageBalances = garageBalances;
            FungibleItemGarages = fungibleItemGarages;
        }
    }

    public GaragesType()
    {
        Field<AddressType>(
            name: "agentAddr",
            resolve: context => context.Source.AgentAddr);
        Field<AddressType>(
            name: "garageBalancesAddr",
            resolve: context => context.Source.GarageBalancesAddr);
        Field<ListGraphType<Libplanet.Explorer.GraphTypes.FungibleAssetValueType>>(
            name: "garageBalances",
            resolve: context => context.Source.GarageBalances);
        Field<ListGraphType<FungibleItemGarageWithAddressType>>(
            name: "fungibleItemGarages",
            resolve: context => context.Source.FungibleItemGarages);
    }
}
