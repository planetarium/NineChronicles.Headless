using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item;

public class FungibleItemType : ItemType<IFungibleItem?>
{
    public FungibleItemType()
    {
        Field<NonNullGraphType<StringGraphType>>(
            "fungibleItemId",
            resolve: context => context.Source?.FungibleId.ToString());
    }
}
