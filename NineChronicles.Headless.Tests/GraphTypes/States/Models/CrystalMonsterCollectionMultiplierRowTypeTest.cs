using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class CrystalMonsterCollectionMultiplierRowTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                level
                multiplier
            }";
            var queryResult = await ExecuteQueryAsync<CrystalMonsterCollectionMultiplierRowType>(
                query,
                source: Fixtures.TableSheetsFX.CrystalMonsterCollectionMultiplierSheet.OrderedList.First()
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);
            Assert.Equal(new Dictionary<string, object>
            {
                ["level"] = 0,
                ["multiplier"] = 0,
            }, data);
        }
    }
}
