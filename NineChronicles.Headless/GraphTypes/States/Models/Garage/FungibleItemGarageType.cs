using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Garages;
using NineChronicles.Headless.GraphTypes.States.Models.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Garage;

public class FungibleItemGarageType : ObjectGraphType<FungibleItemGarage>
{
    public FungibleItemGarageType()
    {
        Field<FungibleItemType>(name: "item", resolve: context => context.Source.Item);
        Field<IntGraphType>(name: "count", resolve: context => context.Source.Count);
    }
}

public class FungibleItemGarageWithAddressType :
    ObjectGraphType<(FungibleItemGarage? fungibleItemGarage, Address addr)>
{
    public FungibleItemGarageWithAddressType()
    {
        Field<FungibleItemType>(
            name: "item",
            resolve: context => context.Source.fungibleItemGarage?.Item);
        Field<IntGraphType>(
            name: "count",
            resolve: context => context.Source.fungibleItemGarage?.Count);
        Field<AddressType>(name: "addr", resolve: context => context.Source.addr);
    }
}
