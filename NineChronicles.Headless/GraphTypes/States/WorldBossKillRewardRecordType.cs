using System.Collections.Generic;
using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class WorldBossKillRewardRecordType : ObjectGraphType<WorldBossKillRewardRecord>
    {
        private class WorldBossKillRewardRecordMapType : ObjectGraphType<KeyValuePair<int, bool>>
        {
            public WorldBossKillRewardRecordMapType()
            {
                Field<IntGraphType>("bossLevel", resolve: context => context.Source.Key);
                Field<BooleanGraphType>(
                    "claimed",
                    description: "check reward already claimed. if already claimed return true.",
                    resolve: context => context.Source.Value);
            }
        }

        public WorldBossKillRewardRecordType()
        {
            Field<ListGraphType<WorldBossKillRewardRecordMapType>>(
                "map",
                resolve: context => context.Source);
        }
    }
}
