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
            Field<NonNullGraphType<BooleanGraphType>>(
                "locked",
                resolve: context => context.Source.Locked);
            Field<GuidGraphType>(
                "lockId",
                resolve: context =>
                {
                    if (context.Source.Lock is OrderLock orderLock)
                    {
                        return orderLock.OrderId;
                    }

                    return null;
                });
            Field<GuidGraphType>(
                "tradableId",
                resolve: context =>
                {
                    if (context.Source.item is ITradableItem tradableItem)
                    {
                        return tradableItem.TradableId;
                    }

                    return null;
                });
            Field<StringGraphType>(
                "fungibleItemId",
                resolve: context =>
                {
                    if (context.Source.item is IFungibleItem fungibleItem)
                    {
                        return fungibleItem.FungibleId.ToString();
                    }

                    return null;
                });
        }
    }
}
