using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakeStateTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task Query(StakeState stakeState, Dictionary<string, object> expected)
        {
            const string query = @"
            {
                address
                startedBlockIndex
                receivedBlockIndex
                cancellableBlockIndex
                claimableBlockIndex
            }";
            var queryResult = await ExecuteQueryAsync<StakeStateType>(query, source: stakeState);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 0),
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["startedBlockIndex"] = 0,
                    ["cancellableBlockIndex"] = StakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0,
                    ["claimableBlockIndex"] = StakeState.RewardInterval,
                }
            }
        };
    }
}
