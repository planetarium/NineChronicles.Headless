using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenWorldType : ObjectGraphType<WorldOpenWorldInfo>
    {
        public WorldOpenWorldType()
        {
            Name = "WorldOpenWorld";
            Field<NonNullGraphType<IntGraphType>>("worldId", resolve: context => context.Source.WorldId);
            Field<NonNullGraphType<IntGraphType>>("stageBegin", resolve: context => context.Source.StageBegin);
            Field<NonNullGraphType<IntGraphType>>("stageEnd", resolve: context => context.Source.StageEnd);
            Field<NonNullGraphType<IntGraphType>>(
                "openedStageEnd",
                resolve: context => context.Source.OpenedStageEnd);
        }
    }
}
