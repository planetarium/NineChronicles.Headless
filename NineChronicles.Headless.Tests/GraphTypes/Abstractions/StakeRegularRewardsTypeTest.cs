using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.Abstractions;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.Abstractions
{
    public class StakeRegularRewardsTypeTest
    {
        [Theory]
        [InlineData(true, 3)]
        [InlineData(false, 2)]
        public async Task Query(bool patched, int expectedCount)
        {
            const string query = @"
            {
                level
                requiredGold
                rewards {
                    itemId
                    rate
                }
                bonusRewards {
                    itemId
                    count
                }
            }";

            const int level = 1;
            StakeRegularRewardSheet rewardSheet = new StakeRegularRewardSheet();
            if (patched)
            {
                rewardSheet.Set(@"level,required_gold,item_id,rate,type
1,50,400000,10,Item
1,50,500000,800,Item
1,50,2002,6000,Rune
2,500,400000,8,Item
2,500,500000,800,Item
2,500,2002,6000,Rune
3,5000,400000,5,Item
3,5000,500000,800,Item
3,5000,2002,6000,Rune
4,50000,400000,5,Item
4,50000,500000,800,Item
4,50000,2002,6000,Rune
5,500000,400000,5,Item
5,500000,500000,800,Item
5,500000,2002,6000,Rune
");
            }
            else
            {
                rewardSheet.Set(@"level,required_gold,item_id,rate
1,50,400000,10
1,50,500000,800
2,500,400000,8
2,500,500000,800
3,5000,400000,5
3,5000,500000,800
4,50000,400000,5
4,50000,500000,800
5,500000,400000,5
5,500000,500000,800
");
            }
            StakeRegularRewardSheet.Row rewardRow = rewardSheet[level]!;
            StakeRegularFixedRewardSheet.Row fixedRewardRow = Fixtures.TableSheetsFX.StakeRegularFixedRewardSheet[level]!;
            StakeRegularRewardSheet.RewardInfo[] rewards = rewardRow.Rewards.ToArray();
            StakeRegularFixedRewardSheet.RewardInfo[] bonusRewards = fixedRewardRow.Rewards.ToArray();
            Assert.Equal(expectedCount, rewards.Length);
            Assert.Single(bonusRewards);
            object[] expectedRewards;
            if (patched)
            {
                expectedRewards = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards[0].ItemId,
                        ["rate"] = rewards[0].Rate,
                    },
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards[1].ItemId,
                        ["rate"] = rewards[1].Rate,
                    },
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards[2].ItemId,
                        ["rate"] = rewards[2].Rate,
                    },
                };
            }
            else
            {
                expectedRewards = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards[0].ItemId,
                        ["rate"] = rewards[0].Rate,
                    },
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards[1].ItemId,
                        ["rate"] = rewards[1].Rate,
                    },
                };
            }
            var queryResult = await ExecuteQueryAsync<StakeRegularRewardsType>(
                query,
                source: (rewardRow.Level, rewardRow.RequiredGold, rewards, bonusRewards)
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var expected = new Dictionary<string, object>
            {
                ["level"] = rewardRow.Level,
                ["requiredGold"] = rewardRow.RequiredGold,
                ["rewards"] = expectedRewards,
                ["bonusRewards"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = bonusRewards[0].ItemId,
                        ["count"] = bonusRewards[0].Count,
                    },
                }
            };
            Assert.Equal(data, expected);
        }
    }
}
