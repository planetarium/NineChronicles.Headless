using System.Collections.Generic;
using System.Threading.Tasks;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class RankingMapStateTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task QueryRankingMapState(RankingMapState rankingMapState, object expected)
        {
            const string query = @"
            {
                address
                capacity
                rankingInfos {
                    agentAddress
                    avatarAddress
                }
            }";

            var queryResult = await ExecuteQueryAsync<RankingMapStateType>(query, source: rankingMapState);
            Assert.Equal(expected, queryResult.Data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new RankingMapState(RankingState.Derive(0)),
                new Dictionary<string, object>
                {
                    ["address"] = RankingState.Derive(0).ToString(),
                    ["capacity"] = RankingMapState.Capacity,
                    ["rankingInfos"] = new List<object>(),
                },
            },
        };
    }
}
