using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.World;

public class ClearedStageType : ObjectGraphType<(int, int)>
{
    public ClearedStageType()
    {
        Field<NonNullGraphType<IntGraphType>>("worldId", resolve: context => context.Source.Item1);
        Field<NonNullGraphType<IntGraphType>>("stageId", resolve: context => context.Source.Item2);
    }
}
