using GraphQL.Types;
using Nekoyume.Model.Skill;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models
{
    public class SkillType : ObjectGraphType<Skill>
    {
        public SkillType()
        {
            Field<NonNullGraphType<IntGraphType>>("id")
                .Resolve(context => context.Source.SkillRow.Id);
            Field<NonNullGraphType<ElementalTypeEnumType>>("elementalType")
                .Resolve(context => context.Source.SkillRow.ElementalType);
            Field<NonNullGraphType<IntGraphType>>(nameof(Skill.Power));
            Field<NonNullGraphType<IntGraphType>>(nameof(Skill.Chance));
        }
    }
}
