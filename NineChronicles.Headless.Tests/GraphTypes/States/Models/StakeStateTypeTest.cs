using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
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
        public async Task Query(StakeState stakeState, long deposit, Dictionary<string, object> expected)
        {
            var goldCurrency = new Currency("NCG", 2, minter: null);

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
            var queryResult = await ExecuteQueryAsync<StakeStateType>(query, source: new StakeStateType.StakeStateContext(stakeState, GetStatesMock, GetBalanceMock, 1));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(expected, data);
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                new StakeState(Fixtures.StakeStateAddress, 0),
                100,
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
                new Dictionary<string, object>
                {
                    ["address"] = Fixtures.StakeStateAddress.ToString(),
                    ["deposit"] = "100.00",
                    ["startedBlockIndex"] = 100,
                    ["cancellableBlockIndex"] = StakeState.LockupInterval + 100,
                    ["receivedBlockIndex"] = 0,
                    ["claimableBlockIndex"] = StakeState.RewardInterval + 100,
                }
            }
        };
    }
}
