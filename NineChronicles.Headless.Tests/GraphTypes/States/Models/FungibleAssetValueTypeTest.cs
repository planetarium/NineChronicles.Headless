using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Assets;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class FungibleAssetValueTypeTest
    {
        [Theory]
        [InlineData(100, 0, "100.00")]
        [InlineData(0, 2, "0.02")]
        [InlineData(20, 2, "20.02")]
        public async Task Query(int major, int minor, string decimalString)
        {
            const string query = @"
            {
                currency
                quantity
            }";
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618
            var fav = new FungibleAssetValue(goldCurrency, major, minor);
            var queryResult = await ExecuteQueryAsync<FungibleAssetValueType>(query, source: fav);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(new Dictionary<string, object>
            {
                ["currency"] = "NCG",
                ["quantity"] = decimalString,
            }, data);
        }
    }
}
