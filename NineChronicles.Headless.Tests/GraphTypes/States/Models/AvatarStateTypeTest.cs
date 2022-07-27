using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet.Assets;
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
                index
            }";
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    avatarState,
                    addresses =>
                    {
                        var arr = new IValue?[addresses.Count];
                        for (int i = 0; i < addresses.Count; i++)
                        {
                            arr[i] = null;

                            if (addresses[i].Equals(Fixtures.AvatarAddress))
                            {
                                arr[i] = Fixtures.AvatarStateFX.Serialize();
                            }
                            
                            if (addresses[i].Equals(Fixtures.UserAddress))
                            {
                                arr[i] = Fixtures.AgentStateFx.Serialize();
                            }
                        }

                        return arr;
                    },
                    (_, _) => new FungibleAssetValue()));
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
                    ["index"] = 2,
                },
            },
        };
    }
}
