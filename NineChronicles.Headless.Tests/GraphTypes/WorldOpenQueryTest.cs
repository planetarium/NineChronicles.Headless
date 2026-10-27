using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet.Action.State;
using Libplanet.Mocks;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Module;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models.WorldOpen;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class WorldOpenQueryTest
    {
        private const string WorldCsv = "id,name,stage_begin,stage_end\n12,Test,511,600";
        private const string CounterCsv =
            "id,type,target_begin,target_end,min_avatar_level\n1,STAGE_PLAY,10,10,0";
        private const string OpenCsv =
            "id,world_id,stage_end,prerequisite_open_id,min_block_index,fallback_block_index,effective_delay_block\n" +
            "1,12,570,,0,1000,0";
        private const string ConditionCsv =
            "open_id,condition_group,counter_id,threshold\n1,1,1,5";

        private const string Fields =
            "evaluatedBlockIndex errors " +
            "worlds { worldId stageBegin stageEnd openedStageEnd } " +
            "opens { id worldId stageEnd confirmedBlockIndex appliedBlockIndex applied pending " +
            "conditionGroups { satisfied conditions { counterId type current threshold } } }";

        private static IWorld Set<T>(IWorld world, string csv) where T : ISheet, new()
        {
            // Record identities as the patch action would have, so draft identity checks have a baseline.
            world = WorldOpenIdentity.CheckAndRecord(world, typeof(T).Name, csv);
            return world.SetLegacyState(Addresses.GetSheetAddress<T>(), (Text)csv);
        }

        private static IWorld LiveWorld(bool withSheets = true)
        {
            IWorld world = new World(MockWorldState.CreateModern());
            world = world.SetLegacyState(Addresses.GetSheetAddress<WorldSheet>(), (Text)WorldCsv);
            if (!withSheets)
            {
                return world;
            }

            world = Set<GlobalCounterSheet>(world, CounterCsv);
            world = Set<WorldOpenSheet>(world, OpenCsv);
            world = Set<WorldOpenConditionSheet>(world, ConditionCsv);
            return world.AddCounter(1, 5).ConfirmOpen(1, 3);
        }

        private static async Task<Dictionary<string, object>> Query(
            IWorld world, long tip, string args = "")
        {
            var result = await ExecuteQueryAsync<StateQuery>(
                $"{{ worldOpen{args} {{ {Fields} }} }}",
                source: new StateContext(world, tip, new StateMemoryCache()));
            Assert.Null(result.Errors);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            return (Dictionary<string, object>)data["worldOpen"];
        }

        private static string Arg(string name, string csv) =>
            $"{name}: \"{csv.Replace("\n", "\\n")}\"";

        private static object World12(Dictionary<string, object> r) =>
            ((IEnumerable<object>)r["worlds"]).Cast<Dictionary<string, object>>()
                .Single(w => (int)w["worldId"] == 12)["openedStageEnd"];

        [Fact]
        public async Task NoSheets_ReturnsEmptyOpens()
        {
            var r = await Query(LiveWorld(withSheets: false), 9);
            Assert.Empty((IEnumerable<object>)r["opens"]);
            Assert.Empty((IEnumerable<object>)r["errors"]);
            Assert.Equal(520, World12(r));
        }

        [Fact]
        public async Task NoSheets_NoWorldSheet_ReturnsEmpty()
        {
            var r = await Query(new World(MockWorldState.CreateModern()), 9);
            Assert.Empty((IEnumerable<object>)r["worlds"]);
            Assert.Empty((IEnumerable<object>)r["opens"]);
        }

        [Fact]
        public async Task Live_ReportsCountersConfirmationAndApplied()
        {
            var r = await Query(LiveWorld(), 9);
            Assert.Equal(10L, r["evaluatedBlockIndex"]);
            Assert.Empty((IEnumerable<object>)r["errors"]);
            Assert.Equal(570, World12(r));
            var open = Assert.Single(((IEnumerable<object>)r["opens"]).Cast<Dictionary<string, object>>());
            Assert.Equal(3L, open["confirmedBlockIndex"]);
            Assert.Equal(3L, open["appliedBlockIndex"]);
            Assert.True((bool)open["applied"]);
            Assert.False((bool)open["pending"]);
            var group = Assert.Single(((IEnumerable<object>)open["conditionGroups"]).Cast<Dictionary<string, object>>());
            Assert.True((bool)group["satisfied"]);
            var cond = Assert.Single(((IEnumerable<object>)group["conditions"]).Cast<Dictionary<string, object>>());
            Assert.Equal(1, cond["counterId"]);
            Assert.Equal("STAGE_PLAY", cond["type"]);
            Assert.Equal(5L, cond["current"]);
            Assert.Equal(5L, cond["threshold"]);
        }

        [Fact]
        public async Task Draft_ClosingOpenedSegment_ShowsLowerStageEnd()
        {
            var draft = OpenCsv.Replace("1,12,570", "1,12,560");
            var r = await Query(LiveWorld(), 9, $"({Arg("worldOpenSheetCsv", draft)})");
            Assert.Single((IEnumerable<object>)r["errors"]);
            Assert.Equal(570, World12(r));
        }

        [Fact]
        public async Task Draft_RaisingMin_ClosesWorld()
        {
            var draft = OpenCsv.Replace("1,12,570,,0,1000,0", "1,12,570,,500,1000,0");
            var r = await Query(LiveWorld(), 9, $"({Arg("worldOpenSheetCsv", draft)})");
            Assert.Empty((IEnumerable<object>)r["errors"]);
            Assert.Equal(520, World12(r));
        }

        [Fact]
        public async Task Draft_Malformed_ReturnsErrorNotGraphQLError()
        {
            var draft = "id,world,end\n1,12,570";
            var r = await Query(LiveWorld(), 9, $"({Arg("worldOpenSheetCsv", draft)})");
            Assert.Single((IEnumerable<object>)r["errors"]);
            Assert.Equal(570, World12(r));
        }

        [Fact]
        public async Task Draft_LoweringThreshold_ConfirmsAtEvaluatedBlock()
        {
            var world = LiveWorld(withSheets: false);
            world = Set<GlobalCounterSheet>(world, CounterCsv);
            world = Set<WorldOpenSheet>(world, OpenCsv);
            world = Set<WorldOpenConditionSheet>(world, ConditionCsv.Replace(",1,5", ",1,10"));
            world = world.AddCounter(1, 5);
            var draft = ConditionCsv;
            var r = await Query(world, 9, $"({Arg("worldOpenConditionSheetCsv", draft)})");
            Assert.Empty((IEnumerable<object>)r["errors"]);
            var open = Assert.Single(((IEnumerable<object>)r["opens"]).Cast<Dictionary<string, object>>());
            Assert.Equal(10L, open["confirmedBlockIndex"]);
            Assert.False((bool)open["pending"]);

            // Without the draft it stays pending.
            var live = await Query(world, 9);
            var liveOpen = Assert.Single(((IEnumerable<object>)live["opens"]).Cast<Dictionary<string, object>>());
            Assert.Null(liveOpen["confirmedBlockIndex"]);
            Assert.True((bool)liveOpen["pending"]);
        }

        [Fact]
        public async Task Draft_TooLarge_ReturnsErrorAndKeepsLive()
        {
            var draft = OpenCsv + "\n" + new string('x', WorldOpenEvaluator.MaxDraftCsvBytes);
            var r = await Query(LiveWorld(), 9, $"({Arg("worldOpenSheetCsv", draft)})");
            var error = Assert.Single(((IEnumerable<object>)r["errors"]).Cast<string>());
            Assert.StartsWith("WorldOpenSheet: draft is too large (", error);
            Assert.Contains($"max {WorldOpenEvaluator.MaxDraftCsvBytes})", error);
            Assert.Equal(570, World12(r));
        }

        [Fact]
        public async Task Draft_AllThreeTogether_AddsNewCounterAndOpen()
        {
            var counters = CounterCsv + "\n2,STAGE_PLAY,11,11,0";
            var opens = OpenCsv + "\n2,12,580,1,0,1000,0";
            var conditions = ConditionCsv + "\n2,1,2,3";
            var args = "(" + Arg("globalCounterSheetCsv", counters) + ", "
                + Arg("worldOpenSheetCsv", opens) + ", "
                + Arg("worldOpenConditionSheetCsv", conditions) + ")";
            var r = await Query(LiveWorld(), 9, args);
            Assert.Empty((IEnumerable<object>)r["errors"]);
            var added = ((IEnumerable<object>)r["opens"]).Cast<Dictionary<string, object>>()
                .Single(o => (int)o["id"] == 2);
            Assert.True((bool)added["pending"]);
            Assert.False((bool)added["applied"]);
        }

        [Fact]
        public async Task Condition_WithMissingCounterRow_HasNullType()
        {
            var world = LiveWorld(withSheets: false);
            world = Set<WorldOpenSheet>(world, OpenCsv);
            world = Set<WorldOpenConditionSheet>(world, ConditionCsv);
            var r = await Query(world, 9);
            var open = Assert.Single(((IEnumerable<object>)r["opens"]).Cast<Dictionary<string, object>>());
            var group = Assert.Single(((IEnumerable<object>)open["conditionGroups"]).Cast<Dictionary<string, object>>());
            var cond = Assert.Single(((IEnumerable<object>)group["conditions"]).Cast<Dictionary<string, object>>());
            Assert.Null(cond["type"]);
        }
    }
}
