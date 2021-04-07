using GraphQL.Types;
using Libplanet.Action;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public abstract class ItemBaseType<T> : ObjectGraphType<(T itemBase, AccountStateGetter accountStateGetter)>
        where T : ItemBase
    {
        protected ItemBaseType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ItemBase.Grade),
                description: "Grade from ItemSheet.",
                resolve: context => context.Source.itemBase.Grade
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ItemBase.Id),
                description: "ID from ItemSheet.",
                resolve: context => context.Source.itemBase.Id
            );
            
            Field<NonNullGraphType<ItemTypeEnumType>>(
                nameof(ItemBase.ItemType),
                description: "Item category.",
                resolve: context => context.Source.itemBase.ItemType
            );
            Field<NonNullGraphType<ItemSubTypeEnumType>>(
                nameof(ItemBase.ItemSubType),
                description: "Item subcategory.",
                resolve: context => context.Source.itemBase.ItemSubType
            );
            Field<NonNullGraphType<ElementalTypeEnumType>>(
                nameof(ItemBase.ElementalType),
                description: "Item elemental.",
                resolve: context => context.Source.itemBase.ElementalType
            );
        }
    }
}
