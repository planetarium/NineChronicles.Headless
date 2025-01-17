using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Mocks;
using Libplanet.Types.Assets;
using Nekoyume;
using Nekoyume.Model.Stake;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakeStateTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task Query(
            Address agentAddress,
            StakeState stakeState,
            Address stakeStateAddress,
            long deposit,
            long blockIndex,
            Dictionary<string, object> expected)
        {
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618

            MockWorldState mockWorldState = MockWorldState.CreateModern();
            mockWorldState = mockWorldState
                .SetAccount(
                    ReservedAddresses.LegacyAccount,
                    new Account(mockWorldState.GetAccountState(ReservedAddresses.LegacyAccount))
                        .SetState(GoldCurrencyState.Address, new GoldCurrencyState(goldCurrency).Serialize()))
                .SetBalance(Fixtures.StakeStateAddress, Currencies.GuildGold, (Currencies.GuildGold * deposit).RawValue);

            const string query = @"
            {
                address
                deposit
                startedBlockIndex
                receivedBlockIndex
                cancellableBlockIndex
                claimableBlockIndex
            }";
            var queryResult = await ExecuteQueryAsync<StakeStateType>(
                query,
                source: new StakeStateType.StakeStateContext(
                    agentAddress,
                    stakeState,
                    stakeStateAddress,
                    new World(mockWorldState),
                    blockIndex, new StateMemoryCache()));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                Fixtures.UserAddress,
                new StakeState(
                    new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 0),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 0L,
                    ["cancellableBlockIndex"] = LegacyStakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0L,
                    ["claimableBlockIndex"] = 0L + LegacyStakeState.RewardInterval,
                }
            },
            new object[]
            {
                Fixtures.UserAddress,
                new StakeState(new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 100),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100L,
                    ["cancellableBlockIndex"] = 100 + LegacyStakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0L,
                    ["claimableBlockIndex"] = 100 + LegacyStakeState.RewardInterval,
                }
            },
            new object[]
            {
                Fixtures.UserAddress,
                new StakeState(new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 100),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100L,
                    ["cancellableBlockIndex"] = LegacyStakeState.LockupInterval + 100,
                    ["receivedBlockIndex"] = 0L,
                    ["claimableBlockIndex"] = LegacyStakeState.RewardInterval + 100,
                }
            },
            new object[]
            {
                Fixtures.UserAddress,
                new StakeState(
                    new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 10, 50412),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 10L,
                    ["cancellableBlockIndex"] = 201610L,
                    ["receivedBlockIndex"] = 50412L,
                    ["claimableBlockIndex"] = 100810L,
                }
            },
            new object[]
            {
                Fixtures.UserAddress,
                new StakeState(new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 10, 50412),
                Fixtures.StakeStateAddress,
                100,
                ActionObsoleteConfig.V100290ObsoleteIndex,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 10L,
                    ["cancellableBlockIndex"] = 201610L,
                    ["receivedBlockIndex"] = 50412L,
                    ["claimableBlockIndex"] = 100810L,
                }
            }
        };
    }
}
