using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public partial class StandaloneQueryTest : GraphQLTestBase
    {
        [Fact]
        public async Task ActionTxQuery_CreateTransaction()
        {
            var publicKey = new PrivateKey().PublicKey;
            long nonce = 0;
            var result = await ExecuteQueryAsync($@"
query {{
    actionTxQuery(publicKey: ""{publicKey.ToString()}"", nonce: {nonce}) {{
        stake(amount: 100)
    }}
}}");
            Assert.Null(result.Errors);
            Assert.NotNull(result.Data);
            var data = Assert.IsType<Dictionary<string, object>>(((ExecutionNode)result.Data!).ToValue());
            var actionTxQueryData = Assert.IsType<Dictionary<string, object>>(data["actionTxQuery"]);
            var stake = Assert.IsType<string>(actionTxQueryData["stake"]);
            var tx = TxMarshaler.DeserializeUnsignedTx(ByteUtil.ParseHex(stake));
            Assert.Equal(publicKey, tx.PublicKey);
            Assert.Equal(publicKey.ToAddress(), tx.Signer);
            Assert.Equal(0, tx.Nonce);
            var rawAction = Assert.Single(tx.Actions);
            var action = new NCActionLoader().LoadAction(0, rawAction);
            Assert.IsType<Stake>(action);
        }

        [InlineData("2022-11-18T00:00:00+0000")]
        [InlineData("2022-11-18T00:00:00Z")]
        [InlineData("2022-11-18T00:00:00+0900")]
        [Theory]
        public async Task ActionTxQuery_CreateTransaction_With_Timestamp(string timestamp)
        {
            var publicKey = new PrivateKey().PublicKey;
            long nonce = 0;
            var result = await ExecuteQueryAsync($@"
query {{
    actionTxQuery(publicKey: ""{publicKey.ToString()}"", nonce: {nonce}, timestamp: ""{timestamp}"") {{
        stake(amount: 100)
    }}
}}");
            Assert.Null(result.Errors);
            var data = Assert.IsType<Dictionary<string, object>>(((ExecutionNode)result.Data!).ToValue());
            var actionTxQueryData = Assert.IsType<Dictionary<string, object>>(data["actionTxQuery"]);
            var stake = Assert.IsType<string>(actionTxQueryData["stake"]);
            var tx = TxMarshaler.DeserializeUnsignedTx(ByteUtil.ParseHex(stake));
            Assert.Equal(DateTimeOffset.Parse(timestamp), tx.Timestamp);
        }
    }
}
