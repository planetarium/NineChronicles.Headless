using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class RuneListRowTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                id
                grade
                runeType
                requiredLevel
                usePlace
            }";
            var queryResult = await ExecuteQueryAsync<RuneListRowType>(
                query,
                source: Fixtures.TableSheetsFX.RuneListSheet.OrderedList.First()
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);
            Assert.Equal(new Dictionary<string, object>
            {
                ["id"] = 1001,
                ["grade"] = 2,
                ["runeType"] = "STAT",
                ["requiredLevel"] = 1,
                ["usePlace"] = "RAID",
            }, data);
        }
    }
}
