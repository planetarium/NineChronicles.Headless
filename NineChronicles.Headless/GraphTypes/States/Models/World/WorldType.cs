using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes.States.Models.World
{
    public class WorldType : ObjectGraphType<WorldInformation.World>
    {
        public WorldType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.Id), resolve: context => context.Source.Id);
            Field<NonNullGraphType<StringGraphType>>(nameof(WorldInformation.World.Name), resolve: context => context.Source.Name);
            Field<NonNullGraphType<BooleanGraphType>>(nameof(WorldInformation.World.IsUnlocked), resolve: context => context.Source.IsUnlocked);
            Field<NonNullGraphType<BooleanGraphType>>(nameof(WorldInformation.World.IsStageCleared), resolve: context => context.Source.IsStageCleared);
            Field<NonNullGraphType<LongGraphType>>(nameof(WorldInformation.World.UnlockedBlockIndex), resolve: context => context.Source.UnlockedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(WorldInformation.World.StageClearedBlockIndex), resolve: context => context.Source.StageClearedBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.StageBegin), resolve: context => context.Source.StageBegin);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.StageEnd), resolve: context => context.Source.StageEnd);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.StageClearedId), resolve: context => context.Source.StageClearedId);
        }
    }
}
