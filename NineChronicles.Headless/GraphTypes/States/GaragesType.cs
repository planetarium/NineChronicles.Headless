using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
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
        public readonly FungibleAssetValue[] FungibleAssetValues;
        public readonly (FungibleItemGarage fungibleItemGarage, Address addr)[] FungibleItemGarages;

        public Value(
            Address agentAddr,
            Address garageBalancesAddr,
            IEnumerable<FungibleAssetValue> fungibleAssetValues,
            IEnumerable<(FungibleItemGarage fungibleItemGarage, Address addr)> fungibleItemGarages)
        {
            AgentAddr = agentAddr;
            GarageBalancesAddr = garageBalancesAddr;
            FungibleAssetValues = fungibleAssetValues.ToArray();
            FungibleItemGarages = fungibleItemGarages.ToArray();
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
            resolve: context => context.Source.FungibleAssetValues);
        Field<ListGraphType<WithAddressType<FungibleItemGarageType, FungibleItemGarage>>>(
            name: "fungibleItemGarages",
            resolve: context => context.Source.FungibleItemGarages);
    }
}
