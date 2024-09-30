using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Crypto;
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
                isUnlocked
                startBlockIndex
            }";

            Address address = default;
            CombinationSlotState combinationSlotState = new CombinationSlotState(address, 1);
            var queryResult = await ExecuteQueryAsync<CombinationSlotStateType>(query, source: combinationSlotState);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var expected = new Dictionary<string, object>()
            {
                ["address"] = address.ToString(),
                ["unlockBlockIndex"] = 0L,
                ["isUnlocked"] = true,
                ["startBlockIndex"] = 0L,
            };
            Assert.Equal(expected, data);
        }
    }
}
