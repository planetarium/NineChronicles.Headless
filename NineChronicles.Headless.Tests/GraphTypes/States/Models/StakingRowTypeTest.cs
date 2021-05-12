using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakingRowTypeTest
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
                    quantity
                }
            }";
            StakingSheet.Row row = Fixtures.TableSheetsFX.StakingSheet.First!;
            List<StakingRewardSheet.RewardInfo> rewards = Fixtures.TableSheetsFX.StakingRewardSheet[row.Level].Rewards;
            Assert.Single(rewards);
            var queryResult = await ExecuteQueryAsync<StakingRowType>(
                query,
                source: (row, Fixtures.TableSheetsFX.StakingRewardSheet)
            );
            var expected = new Dictionary<string, object>
            {
                ["level"] = row.Level,
                ["requiredGold"] = row.RequiredGold,
                ["rewards"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards.First().ItemId,
                        ["quantity"] = rewards.First().Quantity,
                    }
                }
            };
            Assert.Equal(expected, queryResult.Data);
        }

    }
}
