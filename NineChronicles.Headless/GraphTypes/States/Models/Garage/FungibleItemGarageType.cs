using GraphQL.Types;
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
