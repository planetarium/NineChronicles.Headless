using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class InventoryItemType : ObjectGraphType<Inventory.Item>
    {
        public InventoryItemType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(Inventory.Item.count),
                description: "A count of item",
                resolve: context => context.Source.count);
            Field<NonNullGraphType<IntGraphType>>(
                "Id",
                description: "An Id of item",
                resolve: context => context.Source.item.Id);
            Field<NonNullGraphType<ItemTypeEnumType>>(
                "itemType",
                description: "An ItemType of item",
                resolve: context => context.Source.item.ItemType);
        }
    }
}
