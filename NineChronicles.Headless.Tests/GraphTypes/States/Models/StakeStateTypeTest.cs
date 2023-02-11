using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Nekoyume.BlockChain.Policy;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakeStateTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task Query(StakeState stakeState, long deposit, long blockIndex, Dictionary<string, object> expected)
        {
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618

            IValue? GetStateMock(Address address)
            {
                if (GoldCurrencyState.Address == address)
                {
                    return new GoldCurrencyState(goldCurrency).Serialize();
                }

                return null;
            }

            IReadOnlyList<IValue?> GetStatesMock(IReadOnlyList<Address> addresses) =>
                addresses.Select(GetStateMock).ToArray();

            FungibleAssetValue GetBalanceMock(Address address, Currency currency)
            {
                if (address == Fixtures.StakeStateAddress)
                {
                    return goldCurrency * deposit;
                }

                return FungibleAssetValue.FromRawValue(currency, 0);
            }

            const string query = @"
            {
                address
                deposit
                startedBlockIndex
                receivedBlockIndex
                cancellableBlockIndex
                claimableBlockIndex
            }";
            var queryResult = await ExecuteQueryAsync<StakeStateType>(query, source: new StakeStateType.StakeStateContext(stakeState, GetStatesMock, GetBalanceMock, blockIndex));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 0),
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 0,
                    ["cancellableBlockIndex"] = StakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0,
                    ["claimableBlockIndex"] = 0 + StakeState.RewardInterval,
                }
            },
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 100),
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100,
                    ["cancellableBlockIndex"] = 100 + StakeState.LockupInterval,
                    ["receivedBlockIndex"] = 0,
                    ["claimableBlockIndex"] = 100 + StakeState.RewardInterval,
                }
            },
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 100),
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100,
                    ["cancellableBlockIndex"] = StakeState.LockupInterval + 100,
                    ["receivedBlockIndex"] = 0,
                    ["claimableBlockIndex"] = StakeState.RewardInterval + 100,
                }
            },
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 10, 50412, 201610, new StakeState.StakeAchievements()),
                100,
                0,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 10,
                    ["cancellableBlockIndex"] = 201610L,
                    ["receivedBlockIndex"] = 50412,
                    ["claimableBlockIndex"] = 100812L,
                }
            },
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 10, 50412, 201610, new StakeState.StakeAchievements()),
                100,
                BlockPolicySource.V100290ObsoleteIndex,
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 10,
                    ["cancellableBlockIndex"] = 201610L,
                    ["receivedBlockIndex"] = 50412,
                    ["claimableBlockIndex"] = 100810L,
                }
            }
        };
    }
}
