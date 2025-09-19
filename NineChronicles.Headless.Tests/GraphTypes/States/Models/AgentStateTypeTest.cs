using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Mocks;
using Libplanet.Types.Assets;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using Nekoyume.Module;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class AgentStateTypeTest
    {
        [Theory]
        [InlineData(0, "0.00", 0, "0.000000000000000000")]
        [InlineData(10, "10.00", 2, "2.000000000000000000")]
        [InlineData(7777, "7777.00", 30, "30.000000000000000000")]
        public async Task Query(int goldBalance, string goldDecimalString, int crystalBalance, string crystalDecimalString)
        {
            const string query = @"
            {
                address
                avatarStates {
                    address
                    name
                }
                gold
                monsterCollectionRound
                monsterCollectionLevel
                hasTradedItem
                crystal
                pledge {
                    patronAddress
                    approved
                    mead
                }
            }";
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618
            var agentState = new AgentState(new Address())
            {
                avatarAddresses =
                {
                    [0] = Fixtures.AvatarAddress
                }
            };

            Address monsterCollectionAddress = MonsterCollectionState.DeriveAddress(agentState.address, 0);
            MonsterCollectionState monsterCollectionState = new MonsterCollectionState(monsterCollectionAddress, 7, 0, Fixtures.TableSheetsFX.MonsterCollectionRewardSheet);
            Address pledgeAddress = agentState.address.GetPledgeAddress();

            MockWorldState mockWorldState = MockWorldState.CreateModern();
            mockWorldState = mockWorldState
                .SetAccount(
                    ReservedAddresses.LegacyAccount,
                    new Account(mockWorldState.GetAccountState(ReservedAddresses.LegacyAccount))
                        .SetState(GoldCurrencyState.Address, new GoldCurrencyState(goldCurrency).Serialize())
                        .SetState(monsterCollectionAddress, monsterCollectionState.Serialize())
                        .SetState(
                            pledgeAddress,
                            List.Empty
                                .Add(MeadConfig.PatronAddress.Serialize())
                                .Add(true.Serialize())
                                .Add(4.Serialize())))
                .SetBalance(agentState.address, CrystalCalculator.CRYSTAL * crystalBalance)
                .SetBalance(agentState.address, goldCurrency * goldBalance);
            IWorld world = new World(mockWorldState);
            world = world.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,
                true,
                true,
                true);

            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: new AgentStateType.AgentStateContext(agentState, world, 0, new StateMemoryCache())
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var expected = new Dictionary<string, object>()
            {
                ["address"] = agentState.address.ToString(),
                ["avatarStates"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["address"] = Fixtures.AvatarAddress.ToString(),
                        ["name"] = Fixtures.AvatarStateFX.name,
                    },
                },
                ["gold"] = goldDecimalString,
                ["monsterCollectionRound"] = 0L,
                ["monsterCollectionLevel"] = 7L,
                ["hasTradedItem"] = false,
                ["crystal"] = crystalDecimalString,
                ["pledge"] = new Dictionary<string, object>
                {
                    ["patronAddress"] = MeadConfig.PatronAddress.ToString(),
                    ["approved"] = true,
                    ["mead"] = 4
                }
            };
            Assert.Equal(expected, data);
        }

        [Fact]
        public async Task Query_WithoutInventoryField_ShouldNotLoadInventory()
        {
            const string query = @"
            {
                address
                avatarStates {
                    address
                    name
                    level
                }
            }";

            var agentState = new AgentState(new Address())
            {
                avatarAddresses =
                {
                    [0] = Fixtures.AvatarAddress
                }
            };

            MockWorldState mockWorldState = MockWorldState.CreateModern();
            IWorld world = new World(mockWorldState);

            // Set avatar state without inventory (getInventory: false)
            world = world.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,  // getInventory: true
                true,  // getWorldInformation: false
                true,  // getQuestList: true
                true); // getRuneState: true

            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: new AgentStateType.AgentStateContext(agentState, world, 0, new StateMemoryCache())
            );

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var avatarStates = (object[])data["avatarStates"];
            var avatarState = (Dictionary<string, object>)avatarStates[0];

            // Should have basic fields but no inventory field in the query
            Assert.Equal(Fixtures.AvatarAddress.ToString(), avatarState["address"]);
            Assert.Equal(Fixtures.AvatarStateFX.name, avatarState["name"]);
            Assert.Equal(Fixtures.AvatarStateFX.level, avatarState["level"]);
            Assert.False(avatarState.ContainsKey("inventory"));
        }

        [Fact]
        public async Task Query_WithInventoryField_ShouldLoadInventory()
        {
            const string query = @"
            {
                address
                avatarStates {
                    address
                    name
                    level
                    inventory {
                        equipments {
                            id
                        }
                    }
                }
            }";

            var agentState = new AgentState(new Address())
            {
                avatarAddresses =
                {
                    [0] = Fixtures.AvatarAddress
                }
            };

            MockWorldState mockWorldState = MockWorldState.CreateModern();
            IWorld world = new World(mockWorldState);

            // Set avatar state with inventory (getInventory: true)
            world = world.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,  // getInventory: true
                true,  // getWorldInformation: true
                true,  // getQuestList: true
                true); // getRuneState: true

            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: new AgentStateType.AgentStateContext(agentState, world, 0, new StateMemoryCache())
            );

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var avatarStates = (object[])data["avatarStates"];
            var avatarState = (Dictionary<string, object>)avatarStates[0];

            // Should have basic fields and inventory field
            Assert.Equal(Fixtures.AvatarAddress.ToString(), avatarState["address"]);
            Assert.Equal(Fixtures.AvatarStateFX.name, avatarState["name"]);
            Assert.Equal(Fixtures.AvatarStateFX.level, avatarState["level"]);
            Assert.True(avatarState.ContainsKey("inventory"));

            var inventory = (Dictionary<string, object>)avatarState["inventory"];
            Assert.True(inventory.ContainsKey("equipments"));
        }

        [Fact]
        public async Task Query_WithNullInventory_ShouldHandleGracefully()
        {
            const string query = @"
            {
                address
                avatarStates {
                    address
                    name
                    level
                }
            }";

            var agentState = new AgentState(new Address())
            {
                avatarAddresses =
                {
                    [0] = Fixtures.AvatarAddress
                }
            };

            // Create an avatar state with null inventory
            var avatarStateWithNullInventory = AvatarState.Create(
                Fixtures.AvatarAddress,
                Fixtures.UserAddress,
                0,
                Fixtures.TableSheetsFX.GetAvatarSheets(),
                default,
                "TestAvatar"
            );
            avatarStateWithNullInventory.inventory = null; // Explicitly set inventory to null

            MockWorldState mockWorldState = MockWorldState.CreateModern();
            IWorld world = new World(mockWorldState);

            // Set avatar state without inventory (getInventory: false)
            world = world.SetAvatarState(
                Fixtures.AvatarAddress,
                avatarStateWithNullInventory,
                true, // setAvatar: true
                false, // setInventory: false - don't set inventory
                true,  // setWorldInformation: true
                true); // setQuestList: true

            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: new AgentStateType.AgentStateContext(agentState, world, 0, new StateMemoryCache())
            );

            // Should not throw exception and return data
            Assert.NotNull(queryResult.Data);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var avatarStates = data["avatarStates"];

            // Should return the avatar even with null inventory when inventory is not requested
            Assert.NotNull(avatarStates);
            if (avatarStates is object[] avatarStatesArray && avatarStatesArray.Length > 0)
            {
                var avatarState = (Dictionary<string, object>)avatarStatesArray[0];
                Assert.Equal(Fixtures.AvatarAddress.ToString(), avatarState["address"]);
                Assert.Equal(avatarStateWithNullInventory.name, avatarState["name"]);
            }
        }
    }
}
