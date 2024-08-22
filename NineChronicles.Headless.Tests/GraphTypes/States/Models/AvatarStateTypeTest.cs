using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Mocks;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.Module;
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
            IWorld mockWorld = new World(MockWorldState.CreateModern());
            mockWorld = mockWorld.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,
                true,
                true,
                true);
            mockWorld = mockWorld.SetAgentState(Fixtures.UserAddress, Fixtures.AgentStateFx);
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    avatarState,
                    mockWorld,
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
                    isUnlocked
                    startBlockIndex
                    petId
                }
            }
            ";
            IWorld world = new World(MockWorldState.CreateModern());
            world = world.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,
                true,
                true,
                true);
            world = world.SetAgentState(Fixtures.UserAddress, Fixtures.AgentStateFx);
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    avatarState,
                    world,
                    0, new StateMemoryCache()));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        [Theory]
        [MemberData(nameof(ActionPointMembers))]
        public async Task QueryActionPoint(bool modern, Dictionary<string, object> expected)
        {
            const string query = @"
            {
                actionPoint
                dailyRewardReceivedIndex
            }";
            IWorld mockWorld = new World(MockWorldState.CreateModern());
            mockWorld = mockWorld.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,
                true,
                true,
                true);
            mockWorld = mockWorld.SetAgentState(Fixtures.UserAddress, Fixtures.AgentStateFx);
            if (modern)
            {
                mockWorld = mockWorld.SetDailyRewardReceivedBlockIndex(Fixtures.AvatarAddress, 1L)
                    .SetActionPoint(Fixtures.AvatarAddress, 5);
            }
            var queryResult = await ExecuteQueryAsync<AvatarStateType>(
                query,
                source: new AvatarStateType.AvatarStateContext(
                    Fixtures.AvatarStateFX,
                    mockWorld,
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
                    ["combinationSlots"] = new World(MockWorldState.CreateModern()).GetAllCombinationSlotState(Fixtures.AvatarAddress).Select(x => new Dictionary<string, object?>
                    {
                        ["address"] = x.address.ToString(),
                        ["unlockBlockIndex"] = x.UnlockBlockIndex,
                        ["isUnlocked"] = x.IsUnlocked,
                        ["startBlockIndex"] = x.StartBlockIndex,
                        ["petId"] = x.PetId
                    }).ToArray<object>(),
                }
            }
        };

        public static IEnumerable<object[]> ActionPointMembers = new List<object[]>()
        {
            new object[]
            {
                false,
                new Dictionary<string, object>
                {
                    ["actionPoint"] = Fixtures.AvatarStateFX.actionPoint,
                    ["dailyRewardReceivedIndex"] = Fixtures.AvatarStateFX.dailyRewardReceivedIndex,
                }
            },
            new object[]
            {
                true,
                new Dictionary<string, object>
                {
                    ["actionPoint"] = 5,
                    ["dailyRewardReceivedIndex"] = 1L,
                }
            }
        };
    }
}
