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
                Field<NonNullGraphType<IntGraphType>>("bossLevel", resolve: context => context.Source.Key);
                Field<NonNullGraphType<BooleanGraphType>>(
                    "claimed",
                    description: "check reward already claimed. if already claimed return true.",
                    resolve: context => context.Source.Value);
            }
        }

        public WorldBossKillRewardRecordType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<WorldBossKillRewardRecordMapType>>>>(
                "map",
                resolve: context => context.Source);
        }
    }
}
