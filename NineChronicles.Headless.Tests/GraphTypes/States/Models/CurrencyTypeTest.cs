using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class CurrencyTypeTest
    {
        [Theory]
        [InlineData(2, true)]
        [InlineData(0, false)]
        public async Task Query(byte decimalPlaces, bool mintersExist)
        {
            const string query = @"
{
    ticker
    decimalPlaces
    minters
}
";
            var minters = mintersExist ? ImmutableHashSet<Address>.Empty.Add(default) : null;
            var currency = new Currency("NCG", decimalPlaces: decimalPlaces, minters: minters);
            var queryResult = await ExecuteQueryAsync<CurrencyType>(query, source: currency);
            var data = (Dictionary<string, object>) ((ExecutionNode) queryResult.Data!).ToValue()!;
            Assert.Equal("NCG", data["ticker"]);
            Assert.Equal(decimalPlaces, data["decimalPlaces"]);
            if (mintersExist)
            {
                var minter = Assert.Single((object[]) data["minters"]);
                Assert.Equal(minter, default(Address).ToString());
            }
            else
            {
                Assert.Null(data["minters"]);
            }
        }
    }
}
