using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class EquipmentType : ItemBaseType<Equipment>
    {
        public EquipmentType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(Equipment.SetId));
            Field<NonNullGraphType<DecimalStatType>>(nameof(Equipment.Stat));
            Field<NonNullGraphType<BooleanGraphType>>(nameof(Equipment.Equipped));
            Field<NonNullGraphType<GuidGraphType>>(nameof(Equipment.ItemId));
            Field<NonNullGraphType<IntGraphType>>(nameof(Equipment.level),
                resolve: context => context.Source.level);
            Field<ListGraphType<SkillType>>(nameof(Equipment.Skills));
            Field<ListGraphType<SkillType>>(nameof(Equipment.BuffSkills));
            Field<NonNullGraphType<StatsMapType>>(nameof(Equipment.StatsMap));

            Interface<ItemBaseInterfaceType>();
            
            IsTypeOf = obj => obj is Equipment;
        }
    }
}
