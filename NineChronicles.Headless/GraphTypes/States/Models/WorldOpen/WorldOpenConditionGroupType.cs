using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenConditionGroupType : ObjectGraphType<WorldOpenConditionGroupInfo>
    {
        public WorldOpenConditionGroupType()
        {
            Name = "WorldOpenConditionGroup";
            Field<NonNullGraphType<BooleanGraphType>>("satisfied", resolve: context => context.Source.Satisfied);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WorldOpenConditionType>>>>(
                "conditions",
                resolve: context => context.Source.Conditions);
        }
    }
}
