using System;
using System.Collections.Generic;
using System.Linq;
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
                    (_, _) => new FungibleAssetValue(),
                    0));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        [Theory]
        [MemberData(nameof(CombinationSlotSteteMembers))]
        public async Task QueryWithCombinationSlotState(AvatarState avatarState, Dictionary<string, object> expected)
        {
            const string query = @"
            {
                address
                combinationSlotState {
                    address
                    unlockBlockIndex
                    unlockStage
                    startBlockIndex
                    petId
                }
            }
            ";
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    avatarState,
                    addresses => addresses.Select(x =>
                    {
                        if (x == Fixtures.AvatarAddress) return Fixtures.AvatarStateFX.Serialize();
                        if (x == Fixtures.UserAddress) return Fixtures.AgentStateFx.Serialize();
                        if (x == Fixtures.CombinationSlotAddress) return Fixtures.CombinationSlotStateFx.Serialize();
                        return null;
                    }).ToList(),
                    (_, _) => new FungibleAssetValue(),
                    0));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
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

        public static IEnumerable<object[]> CombinationSlotSteteMembers = new List<object[]>()
        {
            new object[]
            {
                Fixtures.AvatarStateFX,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.AvatarAddress.ToString(),
                    ["combinationSlotState"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["address"] = Fixtures.CombinationSlotAddress.ToString(),
                            ["unlockBlockIndex"] = 0,
                            ["unlockStage"] = 1,
                            ["startBlockIndex"] = 0,
                            ["petId"] = null
                        }
                    }
                }
            }
        };
    }
}
