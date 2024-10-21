using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.World;

public class ClearedStageType : ObjectGraphType<(int WorldId, int StageId)>
{
    public ClearedStageType()
    {
        Field<NonNullGraphType<IntGraphType>>("worldId", resolve: context => context.Source.WorldId);
        Field<NonNullGraphType<IntGraphType>>("stageId", resolve: context => context.Source.StageId);
    }
}
