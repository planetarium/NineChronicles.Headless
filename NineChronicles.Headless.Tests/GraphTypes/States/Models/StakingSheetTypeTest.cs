using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakingSheetTypeTest
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
                        quantity
                    }
                }
            }";
            var queryResult = await ExecuteQueryAsync<StakingSheetType>(
                query,
                source: (Fixtures.TableSheetsFX.StakingSheet, Fixtures.TableSheetsFX.StakingRewardSheet)
            );
            Dictionary<string, object> data = queryResult.Data.As<Dictionary<string, object>>();
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);
        }
    }
}
