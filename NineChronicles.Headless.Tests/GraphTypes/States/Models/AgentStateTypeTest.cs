using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet.Action.State;
using Libplanet.Crypto;
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

            MockAccountState mockAccountState = new MockAccountState()
                .SetState(GoldCurrencyState.Address, new GoldCurrencyState(goldCurrency).Serialize())
                .SetState(monsterCollectionAddress, monsterCollectionState.Serialize())
                .SetState(
                    pledgeAddress,
                    List.Empty
                        .Add(MeadConfig.PatronAddress.Serialize())
                        .Add(true.Serialize())
                        .Add(4.Serialize()))
                .SetBalance(agentState.address, CrystalCalculator.CRYSTAL * crystalBalance)
                .SetBalance(agentState.address, goldCurrency * goldBalance);
            IWorld mockWorld = new MockWorld(new MockWorldState(ImmutableDictionary<Address, IAccount>.Empty.Add(
                ReservedAddresses.LegacyAccount,
                new MockAccount(mockAccountState))));
            mockWorld = mockWorld.SetAvatarState(
                Fixtures.AvatarAddress,
                Fixtures.AvatarStateFX,
                true,
                true,
                true,
                true);

            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: new AgentStateType.AgentStateContext(agentState, mockWorld, 0, new StateMemoryCache())
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
    }
}
