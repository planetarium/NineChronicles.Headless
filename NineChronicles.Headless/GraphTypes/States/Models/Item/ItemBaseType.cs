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
            Field<NonNullGraphType<IntGraphType>>(nameof(ItemBase.Grade))
                .Description("Grade from ItemSheet.");
            Field<NonNullGraphType<IntGraphType>>(nameof(ItemBase.Id))
                .Description("ID from ItemSheet.");

            Field<NonNullGraphType<ItemTypeEnumType>>(nameof(ItemBase.ItemType))
                .Description("Item category.")
                .Resolve(context => context.Source.ItemType);
            Field<NonNullGraphType<ItemSubTypeEnumType>>(nameof(ItemBase.ItemSubType))
                .Description("Item subcategory.");
            Field<NonNullGraphType<ElementalTypeEnumType>>(nameof(ItemBase.ElementalType))
                .Description("Item elemental.");
        }
    }
}
