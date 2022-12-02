using GraphQL;
using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes.States.Models.World
{
    public class WorldInformationType : ObjectGraphType<WorldInformation>
    {
        public WorldInformationType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(nameof(WorldInformation.IsStageCleared))
                .Argument<int>("stageId", false)
                .Resolve(context => context.Source.IsStageCleared(context.GetArgument<int>("stageId")));
            Field<NonNullGraphType<BooleanGraphType>>(nameof(WorldInformation.IsWorldUnlocked))
                .Argument<int>("worldId", false)
                .Resolve(context => context.Source.IsWorldUnlocked(context.GetArgument<int>("worldId")));
            Field<NonNullGraphType<WorldType>>("world")
                .Argument<int>("worldId", false)
                .Resolve(context =>
                {
                    int worldId = context.GetArgument<int>("worldId");
                    return context.Source.TryGetWorld(
                        context.GetArgument<int>("worldId"),
                        out WorldInformation.World world)
                        ? world
                        : throw new ExecutionError($"Failed to fetch world {worldId}.");
                });
        }
    }
}
