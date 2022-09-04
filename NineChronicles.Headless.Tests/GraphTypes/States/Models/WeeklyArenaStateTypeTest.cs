using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;


namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class WeeklyArenaStateTypeTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Query(bool ended)
        {
            const string query = @"
            {
                address
                ended
                orderedArenaInfos {
                    avatarAddress
                    arenaRecord {
                        win
                        lose
                        draw
                    }
                    active
                    dailyChallengeCount
                    score
                }
            }";

            var weeklyArenaState = new WeeklyArenaState(WeeklyArenaState.DeriveAddress(0));
            if (ended)
            {
                weeklyArenaState.End();
            }
            var queryResult = await ExecuteQueryAsync<WeeklyArenaStateType>(query, source: weeklyArenaState);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;

            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["address"] = WeeklyArenaState.DeriveAddress(0).ToString(),
                    ["ended"] = ended,
                    ["orderedArenaInfos"] = new List<object>(),
                },
                data
            );
        }
    }
}
