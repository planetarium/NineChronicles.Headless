using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakeRegularRewardRowTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                level
                requiredGold
                rewards {
                    itemId
                    rate
                }
            }";
            StakeRegularRewardSheet.Row row = Fixtures.TableSheetsFX.StakeRegularRewardSheet.First!;
            List<StakeRegularRewardSheet.RewardInfo> rewards = Fixtures.TableSheetsFX.StakeRegularRewardSheet[row.Level].Rewards;
            Assert.Single(rewards);
            var queryResult = await ExecuteQueryAsync<StakeRegularRewardRowType>(
                query,
                source: row
            );
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
            var expected = new Dictionary<string, object>
            {
                ["level"] = row.Level,
                ["requiredGold"] = row.RequiredGold,
                ["rewards"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards.First().ItemId,
                        ["rate"] = rewards.First().Rate,
                    },
                }
            };
            Assert.Equal(data, expected);
            // Assert.Equal(row.Level, data["level"]);
            // Assert.Equal(row.RequiredGold, data["requiredGold"]);
            // var dataRewards = Assert.IsType<Dictionary<string, object>[]>(data["rewards"]).ToList();
            // Assert.Single(dataRewards);
            // Assert.Equal(rewards.First().ItemId, dataRewards[0]["itemId"]);
            // Assert.Equal(rewards.First().Rate, dataRewards[0]["rate"]);
        }
    }
}
