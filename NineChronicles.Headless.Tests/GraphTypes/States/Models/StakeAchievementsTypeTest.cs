using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakeAchievementsTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task AchievementsByLevel(LegacyStakeState.StakeAchievements achievements, int level, Dictionary<string, object> expected)
        {
            string query = @$"
            {{
                achievementsByLevel(level: {level})
            }}";
            var queryResult = await ExecuteQueryAsync<StakeAchievementsType>(query, source: achievements);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new LegacyStakeState.StakeAchievements(new Dictionary<int, int>
                {
                    [1] = 1,
                    [2] = 3,
                }),
                1,
                new Dictionary<string, object>
                {
                    ["achievementsByLevel"] = 1,
                }
            }
        };
    }
}
