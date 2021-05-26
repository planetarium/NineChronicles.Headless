using System.Collections.Generic;
using System.Threading.Tasks;
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
            var goldCurrency = new Currency("NCG", 2, minter: null);
            var fav = new FungibleAssetValue(goldCurrency, major, minor);
            var queryResult = await ExecuteQueryAsync<FungibleAssetValueType>(query, source: fav);
            Assert.Equal(new Dictionary<string, object>
            {
                ["currency"] = "NCG",
                ["quantity"] = decimal.Parse(decimalString),
            }, queryResult.Data);
        }
    }
}
