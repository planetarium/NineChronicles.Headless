using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class InventoryItemType : ObjectGraphType<Inventory.Item>
    {
        public InventoryItemType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(Inventory.Item.count))
                .Description("A count of item")
                .Resolve(context => context.Source.count);
            Field<NonNullGraphType<IntGraphType>>("Id")
                .Description("An Id of item")
                .Resolve(context => context.Source.item.Id);
            Field<NonNullGraphType<ItemTypeEnumType>>("itemType")
                .Description("An ItemType of item")
                .Resolve(context => context.Source.item.ItemType);
        }
    }
}
