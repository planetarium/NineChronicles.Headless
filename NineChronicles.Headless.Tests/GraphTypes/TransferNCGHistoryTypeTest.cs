using System.Collections.Generic;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Crypto;
using NineChronicles.Headless.GraphTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class TransferNCGHistoryTypeTest
    {
        [Fact]
        public async Task Query()
        {
            Address sender = new PrivateKey().ToAddress(),
                recipient = new PrivateKey().ToAddress();
            Currency currency = new Currency("NCG", 2, minter: null);
            FungibleAssetValue amount = new FungibleAssetValue(currency, 10, 10);
            var result = await GraphQLTestUtils.ExecuteQueryAsync<TransferNCGHistoryType>(
                "{ sender recipient amount }",
                source: new TransferNCGHistory(sender, recipient, amount));
            Assert.Equal(new Dictionary<string, object>
            {
                ["sender"] = sender.ToString(),
                ["recipient"] = recipient.ToString(),
                ["amount"] = amount.GetQuantityString(),
            }, result.Data);
        }
    }
}
