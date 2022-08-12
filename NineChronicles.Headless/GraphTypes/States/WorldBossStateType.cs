using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class WorldBossStateType : ObjectGraphType<WorldBossState>
    {
        public WorldBossStateType()
        {
            Field<IntGraphType>(
                nameof(WorldBossState.Id),
                description: "world boss season id.",
                resolve: context => context.Source.Id
            );
            Field<IntGraphType>(
                nameof(WorldBossState.Level),
                description: "world boss current level.",
                resolve: context => context.Source.Level
            );
            Field<BigIntGraphType>(
                nameof(WorldBossState.CurrentHp),
                description: "world boss current hp.",
                resolve: context => context.Source.CurrentHp
            );
            Field<LongGraphType>(
                nameof(WorldBossState.StartedBlockIndex),
                description: "world boss season started block index.",
                resolve: context => context.Source.StartedBlockIndex
            );
            Field<LongGraphType>(
                nameof(WorldBossState.EndedBlockIndex),
                description: "world boss season ended block index.",
                resolve: context => context.Source.EndedBlockIndex
            );
        }
    }
}
