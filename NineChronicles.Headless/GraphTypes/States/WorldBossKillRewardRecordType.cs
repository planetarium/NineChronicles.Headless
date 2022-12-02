using System.Collections.Generic;
using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class WorldBossKillRewardRecordType : ObjectGraphType<WorldBossKillRewardRecord>
    {
        private sealed class WorldBossKillRewardRecordMapType : ObjectGraphType<KeyValuePair<int, bool>>
        {
            public WorldBossKillRewardRecordMapType()
            {
                Field<NonNullGraphType<IntGraphType>>("bossLevel").Resolve(context => context.Source.Key);
                Field<NonNullGraphType<BooleanGraphType>>("claimed")
                    .Description("check reward already claimed. if already claimed return true.")
                    .Resolve(context => context.Source.Value);
            }
        }

        public WorldBossKillRewardRecordType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WorldBossKillRewardRecordMapType>>>>("map")
                .Resolve(context => context.Source);
        }
    }
}
