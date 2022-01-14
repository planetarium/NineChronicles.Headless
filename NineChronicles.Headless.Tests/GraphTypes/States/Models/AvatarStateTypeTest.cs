using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class AvatarStateTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task Query(AvatarState avatarState, Dictionary<string, object> expected)
        {
            const string query = @"
            {
                address
                agentAddress
            }";
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(query, source: avatarState);
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                Fixtures.AvatarStateFX,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.AvatarAddress.ToString(),
                    ["agentAddress"] = Fixtures.UserAddress.ToString(),
                },
            },
        };
    }
}
