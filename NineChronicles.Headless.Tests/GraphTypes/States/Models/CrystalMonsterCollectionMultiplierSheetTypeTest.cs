using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class CrystalMonsterCollectionMultiplierSheetTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                orderedList {
                    level
                    multiplier
                }
            }";
            var queryResult = await ExecuteQueryAsync<CrystalMonsterCollectionMultiplierSheetType>(
                query,
                source: Fixtures.TableSheetsFX.CrystalMonsterCollectionMultiplierSheet
            );
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);

            var list = new object[]
            {
                new Dictionary<string, object>
                {
                    ["level"] = 0,
                    ["multiplier"] = 0,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 1,
                    ["multiplier"] = 0,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 2,
                    ["multiplier"] = 50,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 3,
                    ["multiplier"] = 100,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 4,
                    ["multiplier"] = 200,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 5,
                    ["multiplier"] = 300,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 6,
                    ["multiplier"] = 300,
                },
                new Dictionary<string, object>
                {
                    ["level"] = 7,
                    ["multiplier"] = 300,
                },
            };
            var expected = new Dictionary<string, object> { { "orderedList", list } };
            Assert.Equal(expected, data);
        }
    }
}
