using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.Tests.Common;
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
                inventoryAddress
            }";
            MockState mockState = MockState.Empty
                .SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.Serialize())
                .SetState(Fixtures.UserAddress, Fixtures.AgentStateFx.Serialize());
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    avatarState,
                    mockState,
                    0, new StateMemoryCache()));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        [Theory]
        [MemberData(nameof(CombinationSlotStateMembers))]
        public async Task QueryWithCombinationSlotState(AvatarState avatarState, Dictionary<string, object> expected)
        {
            const string query = @"
            {
                address
                combinationSlots {
                    address
                    unlockBlockIndex
                    unlockStage
                    startBlockIndex
                    petId
                }
            }
            ";
            MockState mockState = MockState.Empty
                .SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.Serialize())
                .SetState(Fixtures.UserAddress, Fixtures.AgentStateFx.Serialize());

            for (int i = 0; i < Fixtures.AvatarStateFX.combinationSlotAddresses.Count; i++)
            {
                mockState = mockState
                    .SetState(
                        Fixtures.AvatarStateFX.combinationSlotAddresses[i],
                        Fixtures.CombinationSlotStatesFx[i].Serialize());
            }

            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    avatarState,
                    mockState,
                    0, new StateMemoryCache()));
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
                    ["inventoryAddress"] = Fixtures.AvatarAddress.Derive(SerializeKeys.LegacyInventoryKey).ToString(),
                },
            },
        };

        public static IEnumerable<object[]> CombinationSlotStateMembers = new List<object[]>()
        {
            new object[]
            {
                Fixtures.AvatarStateFX,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.AvatarAddress.ToString(),
                    ["combinationSlots"] = Fixtures.CombinationSlotStatesFx.Select(x => new Dictionary<string, object?>
                    {
                        ["address"] = x.address.ToString(),
                        ["unlockBlockIndex"] = x.UnlockBlockIndex,
                        ["unlockStage"] = x.UnlockStage,
                        ["startBlockIndex"] = x.StartBlockIndex,
                        ["petId"] = x.PetId
                    }).ToArray<object>(),
                }
            }
        };
    }
}
