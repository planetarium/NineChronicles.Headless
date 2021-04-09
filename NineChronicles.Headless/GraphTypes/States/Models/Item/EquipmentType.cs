using GraphQL.Types;
using Nekoyume.Battle;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class EquipmentType : ItemBaseType<Equipment>
    {
        public EquipmentType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(Equipment.SetId),
                description: "The effect set id of equipment.",
                resolve: context => context.Source.itemBase.ItemId
            );
            Field<NonNullGraphType<DecimalStatType>>(
                nameof(Equipment.Stat),
                description: "The stat type to increase when equip this equipment.",
                resolve: context => context.Source.itemBase.Stat
            );
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(Equipment.Equipped),
                description: "Status of avatar equipped.",
                resolve: context => context.Source.itemBase.Equipped
            );
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(Equipment.ItemId),
                description: "Guid of equipment.",
                resolve: context => context.Source.itemBase.ItemId
            );
            Field<NonNullGraphType<IntGraphType>>(
                "CombatPoint",
                description: "Combat point of item.",
                resolve: context => CPHelper.GetCP(context.Source.itemBase));
        }
    }
}
