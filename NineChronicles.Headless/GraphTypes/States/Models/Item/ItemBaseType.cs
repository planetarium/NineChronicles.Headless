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
            Field<NonNullGraphType<IntGraphType>>(nameof(ItemBase.Grade));
            Field<NonNullGraphType<IntGraphType>>(nameof(ItemBase.Id));
            
            Field<NonNullGraphType<ItemTypeEnumType>>(nameof(ItemBase.ItemType), resolve: context => context.Source.ItemType);
            Field<NonNullGraphType<ItemSubTypeEnumType>>(nameof(ItemBase.ItemSubType));
            Field<NonNullGraphType<ElementalTypeEnumType>>(nameof(ItemBase.ElementalType));
        }
    }
}
