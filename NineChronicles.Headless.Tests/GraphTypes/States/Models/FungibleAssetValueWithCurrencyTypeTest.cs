using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Assets;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class FungibleAssetValueWithCurrencyTypeTest
    {
        [Theory]
        [InlineData(100, 0, "100")]
        [InlineData(0, 2, "0.02")]
        [InlineData(20, 2, "20.02")]
        public async Task Query(int major, int minor, string decimalString)
        {
            const string query = @"
            {
                currency {
                    decimalPlaces
                    minters
                    ticker
                }
                quantity
            }";
            var goldCurrency = new Currency("NCG", 2, minter: null);
            var fav = new FungibleAssetValue(goldCurrency, major, minor);
            var queryResult = await ExecuteQueryAsync<FungibleAssetValueWithCurrencyType>(query, source: fav);
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
            Assert.Equal(new Dictionary<string, object>
            {
                ["currency"] = new Dictionary<string, object>
                {
                    ["decimalPlaces"] = (byte)2,
                    ["ticker"] = "NCG",
                    ["minters"] = null!,
                },
                ["quantity"] = decimalString,
            }, data);
        }
    }
}
