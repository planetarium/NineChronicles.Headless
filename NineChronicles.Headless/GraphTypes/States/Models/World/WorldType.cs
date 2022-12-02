using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes.States.Models.World
{
    public class WorldType : ObjectGraphType<WorldInformation.World>
    {
        public WorldType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.Id))
                .Resolve(context => context.Source.Id);
            Field<NonNullGraphType<StringGraphType>>(nameof(WorldInformation.World.Name))
                .Resolve(context => context.Source.Name);
            Field<NonNullGraphType<BooleanGraphType>>(nameof(WorldInformation.World.IsUnlocked))
                .Resolve(context => context.Source.IsUnlocked);
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(WorldInformation.World.IsStageCleared))
                .Resolve(context => context.Source.IsStageCleared);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(WorldInformation.World.UnlockedBlockIndex))
                .Resolve(context => context.Source.UnlockedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(WorldInformation.World.StageClearedBlockIndex))
                .Resolve(context => context.Source.StageClearedBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.StageBegin))
                .Resolve(context => context.Source.StageBegin);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.StageEnd))
                .Resolve(context => context.Source.StageEnd);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldInformation.World.StageClearedId))
                .Resolve(context => context.Source.StageClearedId);
        }
    }
}
