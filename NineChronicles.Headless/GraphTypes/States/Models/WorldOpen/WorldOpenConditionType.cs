using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenConditionType : ObjectGraphType<WorldOpenConditionInfo>
    {
        public WorldOpenConditionType()
        {
            Name = "WorldOpenCondition";
            Field<NonNullGraphType<IntGraphType>>("counterId", resolve: context => context.Source.CounterId);
            Field<StringGraphType>("type", resolve: context => context.Source.Type);
            Field<IntGraphType>("targetBegin", resolve: context => context.Source.TargetBegin);
            Field<IntGraphType>("targetEnd", resolve: context => context.Source.TargetEnd);
            Field<IntGraphType>("minAvatarLevel", resolve: context => context.Source.MinAvatarLevel);
            Field<NonNullGraphType<LongGraphType>>("current", resolve: context => context.Source.Current);
            Field<NonNullGraphType<LongGraphType>>("threshold", resolve: context => context.Source.Threshold);
        }
    }
}
