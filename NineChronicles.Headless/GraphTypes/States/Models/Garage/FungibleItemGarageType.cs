using GraphQL.Types;
using Nekoyume.Model.Garages;

namespace NineChronicles.Headless.GraphTypes.States.Models.Garage;

public class FungibleItemGarageType: ObjectGraphType<FungibleItemGarage>
{
    public FungibleItemGarageType()
    {
        Field<StringGraphType>(name: "fungibleItemId", resolve: context => context.Source.Item.FungibleId);
        Field<IntGraphType>(name: "count", resolve: context => context.Source.Count);
    }
}
