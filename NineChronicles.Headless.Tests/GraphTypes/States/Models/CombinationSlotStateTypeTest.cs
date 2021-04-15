using System.Collections.Generic;
using System.Threading.Tasks;
using Libplanet;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class CombinationSlotStateTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                address
                unlockBlockIndex
                unlockStage
                startBlockIndex
            }";

            Address address = default;
            CombinationSlotState combinationSlotState = new CombinationSlotState(address, 1);
            var queryResult = await ExecuteQueryAsync<CombinationSlotStateType>(query, source: combinationSlotState);
            var expected = new Dictionary<string, object>()
            {
                ["address"] = address.ToString(),
                ["unlockBlockIndex"] = 0,
                ["unlockStage"] = 1,
                ["startBlockIndex"] = 0,
            };
            Assert.Equal(expected, queryResult.Data);
        }
    }
}
