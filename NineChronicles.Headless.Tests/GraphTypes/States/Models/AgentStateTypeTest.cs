using System.Collections.Generic;
using System.Threading.Tasks;
using Libplanet;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class AgentStateTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                address
                avatarAddresses
            }";
            var agentState = new AgentState(new Address());
            var queryResult = await ExecuteQueryAsync<AgentStateType>(query, source: agentState);
            var expected = new Dictionary<string, object>()
            {
                ["address"] = agentState.address.ToString(),
                ["avatarAddresses"] = new List<string>()
            };
            Assert.Equal(expected, queryResult.Data);
        }
    }
}
