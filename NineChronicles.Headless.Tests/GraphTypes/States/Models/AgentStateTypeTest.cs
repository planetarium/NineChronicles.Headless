using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
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

            IValue? GetStateMock(Address address)
            {
                if (GoldCurrencyState.Address == address)
                {
                    return new GoldCurrencyState(goldCurrency).Serialize();
                }

                if (monsterCollectionAddress == address)
                {
                    return monsterCollectionState.Serialize();
                }

                if (Fixtures.AvatarAddress == address)
                {
                    return Fixtures.AvatarStateFX.Serialize();
                }

                return null;
            }

            IReadOnlyList<IValue?> GetStatesMock(IReadOnlyList<Address> addresses) =>
                addresses.Select(GetStateMock).ToArray();

            FungibleAssetValue GetBalanceMock(Address address, Currency currency)
            {
                if (address == agentState.address)
                {
                    var balance = currency.Equals(CrystalCalculator.CRYSTAL)
                        ? crystalBalance
                        : goldBalance;
                    return new FungibleAssetValue(currency, balance, 0);
                }

                return FungibleAssetValue.FromRawValue(currency, 0);
            }

            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: new AgentStateType.AgentStateContext(agentState, GetStatesMock, GetBalanceMock, 0)
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
            };
            Assert.Equal(expected, data);
        }
    }
}
