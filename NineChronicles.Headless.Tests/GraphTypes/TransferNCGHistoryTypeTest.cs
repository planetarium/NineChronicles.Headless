using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Tx;
using NineChronicles.Headless.GraphTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class TransferNCGHistoryTypeTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("memo")]
        public async Task Query(string? memo)
        {
            Random random = new Random();
            Address sender = new PrivateKey().ToAddress(),
                recipient = new PrivateKey().ToAddress();
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            Currency currency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618
            byte[] buffer = new byte[HashDigest<SHA256>.Size];
            random.NextBytes(buffer);
            BlockHash blockHash = new BlockHash(buffer);
            buffer = new byte[TxId.Size];
            random.NextBytes(buffer);
            TxId txId = new TxId(buffer);
            FungibleAssetValue amount = new FungibleAssetValue(currency, 10, 10);
            var result = await GraphQLTestUtils.ExecuteQueryAsync<TransferNCGHistoryType>(
                "{ blockHash txId sender recipient amount }",
                source: new TransferNCGHistory(blockHash, txId, sender, recipient, amount, memo));
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Equal(new Dictionary<string, object>
            {
                ["blockHash"] = blockHash.ToString(),
                ["txId"] = txId.ToString(),
                ["sender"] = sender.ToString(),
                ["recipient"] = recipient.ToString(),
                ["amount"] = amount.GetQuantityString(),
            }, data);
        }
    }
}
