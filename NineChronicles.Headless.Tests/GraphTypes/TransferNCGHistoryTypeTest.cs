using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
        [Fact]
        public async Task Query()
        {
            Random random = new Random();
            Address sender = new PrivateKey().ToAddress(),
                recipient = new PrivateKey().ToAddress();
            Currency currency = new Currency("NCG", 2, minter: null);
            byte[] buffer = new byte[HashDigest<SHA256>.Size];
            random.NextBytes(buffer);
            BlockHash blockHash = new BlockHash(buffer);
            buffer = new byte[TxId.Size];
            random.NextBytes(buffer);
            TxId txId = new TxId(buffer);
            FungibleAssetValue amount = new FungibleAssetValue(currency, 10, 10);
            var result = await GraphQLTestUtils.ExecuteQueryAsync<TransferNCGHistoryType>(
                "{ blockHash txId sender recipient amount }",
                source: new TransferNCGHistory(blockHash, txId, sender, recipient, amount));
            Assert.Equal(new Dictionary<string, object>
            {
                ["blockHash"] = blockHash.ToString(),
                ["txId"] = txId.ToString(),
                ["sender"] = sender.ToString(),
                ["recipient"] = recipient.ToString(),
                ["amount"] = amount.GetQuantityString(),
            }, result.Data);
        }
    }
}
