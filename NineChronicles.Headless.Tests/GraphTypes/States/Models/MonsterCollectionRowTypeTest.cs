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
    public class MonsterCollectionRowTypeTest
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
            MonsterCollectionSheet.Row row = Fixtures.TableSheetsFX.MonsterCollectionSheet.First!;
            List<MonsterCollectionRewardSheet.RewardInfo> rewards = Fixtures.TableSheetsFX.MonsterCollectionRewardSheet[row.Level].Rewards;
            Assert.Equal(2, rewards.Count);
            var queryResult = await ExecuteQueryAsync<MonsterCollectionRowType>(
                query,
                source: (row, Fixtures.TableSheetsFX.MonsterCollectionRewardSheet)
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
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
                    },
                    new Dictionary<string, object>
                    {
                        ["itemId"] = rewards.Last().ItemId,
                        ["quantity"] = rewards.Last().Quantity,
                    }
                }
            };
            Assert.Equal(expected, data);
        }

    }
}
