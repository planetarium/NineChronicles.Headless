using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class AgentStateTypeTest
    {
        [Theory]
        [InlineData(0, "0.00")]
        [InlineData(10, "10.00")]
        [InlineData(7777, "7777.00")]
        public async Task Query(int goldBalance, string decimalString)
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
            }";
            var goldCurrency = new Currency("NCG", 2, minter: null);
            var agentState = new AgentState(new Address());
            agentState.avatarAddresses[0] = Fixtures.AvatarAddress;

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

            FungibleAssetValue GetBalanceMock(Address address, Currency currency)
            {
                if (address == agentState.address)
                {
                    return new FungibleAssetValue(currency, goldBalance, 0);   
                }

                return FungibleAssetValue.FromRawValue(currency, 0);
            }
            
            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: (
                    agentState,
                    (AccountStateGetter)GetStateMock,
                    (AccountBalanceGetter)GetBalanceMock)
            );
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
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
                ["gold"] = decimalString,
                ["monsterCollectionRound"] = 0L,
                ["monsterCollectionLevel"] = 7L,
                ["hasTradedItem"] = false,
            };
            Assert.Equal(expected, data);
        }
    }
}
