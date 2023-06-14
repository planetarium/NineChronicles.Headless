using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public abstract class ItemBaseType<T> : ObjectGraphType<T>
        where T : ItemBase
    {
        protected ItemBaseType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ItemBase.Grade),
                description: "Grade from ItemSheet."
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ItemBase.Id),
                description: "ID from ItemSheet."
            );

            Field<NonNullGraphType<ItemTypeEnumType>>(
                nameof(ItemBase.ItemType),
                description: "Item category.",
                resolve: context => context.Source.ItemType
            );
            Field<NonNullGraphType<ItemSubTypeEnumType>>(
                nameof(ItemBase.ItemSubType),
                description: "Item subcategory."
            );
            Field<NonNullGraphType<ElementalTypeEnumType>>(
                nameof(ItemBase.ElementalType),
                description: "Item elemental."
            );
            Field<LongGraphType>(
                "requiredBlockIndex",
                resolve: context =>
                {
                    if (context.Source is ITradableItem tradableItem)
                    {
                        return tradableItem.RequiredBlockIndex;
                    }

                    return null;
                }
            );
        }
    }
}
