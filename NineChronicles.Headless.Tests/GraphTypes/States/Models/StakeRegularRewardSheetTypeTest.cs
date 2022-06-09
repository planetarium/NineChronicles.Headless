using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakeRegularRewardSheetTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                orderedList {
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
                }
            }";
            var queryResult = await ExecuteQueryAsync<StakeRegularRewardSheetType>(
                query,
                source: (Fixtures.TableSheetsFX.StakeRegularRewardSheet,
                    Fixtures.TableSheetsFX.StakeRegularFixedRewardSheet)
            );
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);
        }
    }
}
