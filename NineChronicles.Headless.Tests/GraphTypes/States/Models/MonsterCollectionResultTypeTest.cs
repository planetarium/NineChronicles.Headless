using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class MonsterCollectionResultTypeTest
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
            var ri = new MonsterCollectionRewardSheet.RewardInfo("1", "1");
            var result = new MonsterCollectionResult(Guid.Empty, default, new List<MonsterCollectionRewardSheet.RewardInfo>
            {
                ri
            });
            var queryResult = await ExecuteQueryAsync<MonsterCollectionResultType>(
                query,
                source: result
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
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
