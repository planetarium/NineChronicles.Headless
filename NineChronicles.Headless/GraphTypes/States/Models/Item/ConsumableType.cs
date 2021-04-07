using GraphQL.Types;
using Nekoyume.Battle;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ConsumableType : ItemBaseType<Consumable>
    {
        public ConsumableType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(Consumable.ItemId),
                description: "Guid of food.",
                resolve: context => context.Source.itemBase.ItemId
            );
            Field<NonNullGraphType<StatTypeEnumType>>(
                nameof(Consumable.MainStat),
                description: "Increase stat type when eat this food.",
                resolve: context => context.Source.itemBase.MainStat
            );
            Field<NonNullGraphType<IntGraphType>>(
                "CombatPoint",
                description: "Combat point of item.",
                resolve: context => CPHelper.GetCP(context.Source.itemBase));
        }
    }
}
