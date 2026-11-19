using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenEntryType : ObjectGraphType<WorldOpenEntryInfo>
    {
        public WorldOpenEntryType()
        {
            Name = "WorldOpenEntry";
            Field<NonNullGraphType<IntGraphType>>("id", resolve: context => context.Source.Id);
            Field<NonNullGraphType<IntGraphType>>("worldId", resolve: context => context.Source.WorldId);
            Field<NonNullGraphType<IntGraphType>>("stageEnd", resolve: context => context.Source.StageEnd);
            Field<IntGraphType>("prerequisiteOpenId", resolve: context => context.Source.PrerequisiteOpenId);
            Field<NonNullGraphType<LongGraphType>>("minBlockIndex", resolve: context => context.Source.MinBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "fallbackBlockIndex",
                resolve: context => context.Source.FallbackBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "effectiveDelayBlock",
                resolve: context => context.Source.EffectiveDelayBlock);
            Field<LongGraphType>(
                "confirmedBlockIndex",
                description: "Null while unconfirmed.",
                resolve: context => context.Source.ConfirmedBlockIndex);
            Field<LongGraphType>(
                "appliedBlockIndex",
                description: "Null when the prerequisite chain can never apply.",
                resolve: context => context.Source.AppliedBlockIndex);
            Field<NonNullGraphType<BooleanGraphType>>("applied", resolve: context => context.Source.Applied);
            Field<NonNullGraphType<BooleanGraphType>>("pending", resolve: context => context.Source.Pending);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WorldOpenConditionGroupType>>>>(
                "conditionGroups",
                resolve: context => context.Source.ConditionGroups);
        }
    }
}
