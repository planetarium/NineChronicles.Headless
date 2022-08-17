using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class WorldBossStateType : ObjectGraphType<WorldBossState>
    {
        public WorldBossStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(WorldBossState.Id),
                description: "world boss season id.",
                resolve: context => context.Source.Id
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(WorldBossState.Level),
                description: "world boss current level.",
                resolve: context => context.Source.Level
            );
            Field<NonNullGraphType<BigIntGraphType>>(
                nameof(WorldBossState.CurrentHp),
                description: "world boss current hp.",
                resolve: context => context.Source.CurrentHp
            );
            Field<NonNullGraphType<LongGraphType>>(
                nameof(WorldBossState.StartedBlockIndex),
                description: "world boss season started block index.",
                resolve: context => context.Source.StartedBlockIndex
            );
            Field<NonNullGraphType<LongGraphType>>(
                nameof(WorldBossState.EndedBlockIndex),
                description: "world boss season ended block index.",
                resolve: context => context.Source.EndedBlockIndex
            );
        }
    }
}
