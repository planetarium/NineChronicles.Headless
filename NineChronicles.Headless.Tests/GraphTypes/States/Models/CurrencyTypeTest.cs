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
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var currency = Currency.Legacy("NCG", decimalPlaces: decimalPlaces, minters: minters);
#pragma warning restore CS0618
            var queryResult = await ExecuteQueryAsync<CurrencyType>(query, source: currency);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal("NCG", data["ticker"]);
            Assert.Equal(decimalPlaces, data["decimalPlaces"]);
            if (mintersExist)
            {
                var minter = Assert.Single((object[])data["minters"]);
                Assert.Equal(minter, default(Address).ToString());
            }
            else
            {
                Assert.Null(data["minters"]);
            }
        }
    }
}
