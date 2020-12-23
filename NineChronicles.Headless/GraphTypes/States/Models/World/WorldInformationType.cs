using GraphQL;
using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes.States.Models.World
{
    public class WorldInformationType : ObjectGraphType<WorldInformation>
    {
        public WorldInformationType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(WorldInformation.IsStageCleared),
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "stageId",
                    }),
                resolve: context => context.Source.IsStageCleared(context.GetArgument<int>("stageId")));
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(WorldInformation.IsWorldUnlocked),
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "worldId",
                    }),
                resolve: context => context.Source.IsStageCleared(context.GetArgument<int>("worldId")));
            Field<NonNullGraphType<WorldType>>(
                "world",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "worldId",
                    }),
                resolve: context =>
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
