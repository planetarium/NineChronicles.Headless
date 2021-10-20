using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ItemBaseInterfaceType : InterfaceGraphType<ItemBase>
    {
        public ItemBaseInterfaceType()
        {
            Field<NonNullGraphType<IntGraphType>>("id", resolve: ctx => ctx.Source.Id);
            Field<NonNullGraphType<IntGraphType>>("grade", resolve: ctx => ctx.Source.Grade);
            Field<NonNullGraphType<ItemTypeEnumType>>("itemType", resolve: ctx => ctx.Source.ItemType);
            Field<NonNullGraphType<ItemSubTypeEnumType>>("itemSubType", resolve: ctx => ctx.Source.ItemSubType);
            Field<NonNullGraphType<ElementalTypeEnumType>>("elementalType", resolve: ctx => ctx.Source.ElementalType);
        }
    }
}
