using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Action.State;
using Libplanet.Crypto;
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
        public async Task Query(StakeStateV2 stakeState, Address stakeStateAddress, long deposit, long blockIndex, Dictionary<string, object> expected)
        {
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618

            MockAccountState mockAccountState = new MockAccountState()
                .SetState(GoldCurrencyState.Address, new GoldCurrencyState(goldCurrency).Serialize())
                .SetBalance(Fixtures.StakeStateAddress, goldCurrency, (goldCurrency * deposit).RawValue);

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
                    stakeState,
                    stakeStateAddress,
                    new MockWorld(new MockWorldState(
                        ImmutableDictionary<Address, IAccount>.Empty.Add(
                            ReservedAddresses.LegacyAccount,
                            new MockAccount(mockAccountState)))),
                    blockIndex, new StateMemoryCache()));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new StakeStateV2(
                    new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 0),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 0L,
                    ["cancellableBlockIndex"] = StakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0L,
                    ["claimableBlockIndex"] = 0L + StakeState.RewardInterval,
                }
            },
            new object[]
            {
                new StakeStateV2(new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 100),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100L,
                    ["cancellableBlockIndex"] = 100 + StakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0L,
                    ["claimableBlockIndex"] = 100 + StakeState.RewardInterval,
                }
            },
            new object[]
            {
                new StakeStateV2(new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 100),
                Fixtures.StakeStateAddress,
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100L,
                    ["cancellableBlockIndex"] = StakeState.LockupInterval + 100,
                    ["receivedBlockIndex"] = 0L,
                    ["claimableBlockIndex"] = StakeState.RewardInterval + 100,
                }
            },
            new object[]
            {
                new StakeStateV2(
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
                new StakeStateV2(new Contract("StakeRegularFixedRewardSheet_V1", "StakeRegularRewardSheet_V1", 50400, 201600), 10, 50412),
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
