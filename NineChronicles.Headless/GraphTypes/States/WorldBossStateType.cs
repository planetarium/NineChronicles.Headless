using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class WorldBossStateType : ObjectGraphType<WorldBossState>
    {
        public WorldBossStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldBossState.Id))
                .Description("world boss season id.")
                .Resolve(context => context.Source.Id);
            Field<NonNullGraphType<IntGraphType>>(nameof(WorldBossState.Level))
                .Description("world boss current level.")
                .Resolve(context => context.Source.Level);
            Field<NonNullGraphType<BigIntGraphType>>(nameof(WorldBossState.CurrentHp))
                .Description("world boss current hp.")
                .Resolve(context => context.Source.CurrentHp);
            Field<NonNullGraphType<LongGraphType>>(nameof(WorldBossState.StartedBlockIndex))
                .Description("world boss season started block index.")
                .Resolve(context => context.Source.StartedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(WorldBossState.EndedBlockIndex))
                .Description("world boss season ended block index.")
                .Resolve(context => context.Source.EndedBlockIndex);
        }
    }
}
