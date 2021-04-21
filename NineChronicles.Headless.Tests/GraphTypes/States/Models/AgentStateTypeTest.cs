using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex.Types;
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
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(7777)]
        public async Task Query(int goldBalance)
        {
            const string query = @"
            {
                address
                avatarAddresses
                gold
                stakingRound
                stakingLevel
            }";
            var goldCurrency = new Currency("NCG", 2, minter: null);
            var agentState = new AgentState(new Address());

            Address stakingAddress = StakingState.DeriveAddress(agentState.address, 0);
            StakingState stakingState = new StakingState(stakingAddress, 7, 0, Fixtures.TableSheetsFX.StakingRewardSheet);
            
            IValue? GetStateMock(Address address)
            {
                if (GoldCurrencyState.Address == address)
                {
                    return new GoldCurrencyState(goldCurrency).Serialize();
                }

                if (stakingAddress == address)
                {
                    return stakingState.Serialize();
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
            var expected = new Dictionary<string, object>()
            {
                ["address"] = agentState.address.ToString(),
                ["avatarAddresses"] = new List<string>(),
                ["gold"] = goldBalance.ToString(),
                ["stakingRound"] = 0L,
                ["stakingLevel"] = 7L,
            };
            Assert.Equal(expected, queryResult.Data);
        }
    }
}
