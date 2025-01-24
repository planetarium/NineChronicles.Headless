using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c;
using NineChronicles.Headless.GraphTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class DelegatorTypeTest
    {
        [Fact]
        public async Task ExecuteQuery()
        {
            var lastDistributeHeight = 1L;
            var share = new BigInteger(1000000000000000000) * 10;
            var fav = Currencies.GuildGold * 10;

            var result = await GraphQLTestUtils.ExecuteQueryAsync<DelegatorType>(
                "{ lastDistributeHeight share fav { currency quantity } }",
                source: new DelegatorType
                {
                    LastDistributeHeight = lastDistributeHeight,
                    Share = share,
                    Fav = fav,
                }
            );

            // Then
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;

            Assert.Equal(lastDistributeHeight, data["lastDistributeHeight"]);
            Assert.Equal(share.ToString("N0"), data["share"]);

            var favResult = (Dictionary<string, object>)data["fav"];
            Assert.Equal(fav.Currency.Ticker, favResult["currency"]);
            Assert.Equal(fav.GetQuantityString(minorUnit: true), favResult["quantity"]);
        }
    }
}
