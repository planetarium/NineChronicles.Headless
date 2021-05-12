using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakingResultTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                avatarAddress
                rewards {
                    itemId
                    quantity
                }
            }";
            var ri = new StakingRewardSheet.RewardInfo("1", "1");
            var result = new StakingResult(default, default, new List<StakingRewardSheet.RewardInfo>
            {
                ri
            });
            var queryResult = await ExecuteQueryAsync<StakingResultType>(
                query,
                source: result
            );
            Dictionary<string, object> data = queryResult.Data.As<Dictionary<string, object>>();
            var expected = new Dictionary<string, object>
            {
                ["avatarAddress"] = result.avatarAddress.ToString(),
                ["rewards"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = 1,
                        ["quantity"] = 1,
                    }
                }
            };
            Assert.Equal(expected, data);
            Assert.Null(queryResult.Errors);
        }

    }
}
