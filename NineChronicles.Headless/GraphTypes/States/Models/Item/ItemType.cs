using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item;

public class ItemType<T> : ObjectGraphType<T> where T : IItem?
{
    protected ItemType()
    {
        Field<NonNullGraphType<ItemTypeEnumType>>(
            "itemType",
            description: "Item category.",
            resolve: context => context.Source?.ItemType
        );
        Field<NonNullGraphType<ItemSubTypeEnumType>>(
            "itemSubType",
            description: "Item sub category.",
            resolve: context => context.Source?.ItemSubType
        );
    }
}
