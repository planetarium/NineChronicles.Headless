using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c;
using Libplanet;
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Action;
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
            Assert.Equal(4, tx.GasLimit);
            Assert.Equal(1 * Currencies.Mead, tx.MaxGasPrice);
            var rawAction = Assert.Single(tx.Actions);
#pragma warning disable CS0612
            var action = new PolymorphicAction<ActionBase>();
#pragma warning restore CS0612
            action.LoadPlainValue(rawAction);
            Assert.IsType<Stake>(action.InnerAction);
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

        [Fact]
        public async Task ActionTxQuery_With_Gas()
        {
            var publicKey = new PrivateKey().PublicKey;
            var address = new PrivateKey().ToAddress();
            long nonce = 0;
            var result = await ExecuteQueryAsync($@"
query {{
    actionTxQuery(publicKey: ""{publicKey.ToString()}"", nonce: {nonce}, gasLimit: 1, maxGasPrice: {{ quantity: 1, decimalPlaces: 18, ticker: ""Mead"" }}) {{
        requestPledge(agentAddress: ""{address}"")
    }}
}}");
            Assert.Null(result.Errors);
            var data = Assert.IsType<Dictionary<string, object>>(((ExecutionNode)result.Data!).ToValue());
            var actionTxQueryData = Assert.IsType<Dictionary<string, object>>(data["actionTxQuery"]);
            var stake = Assert.IsType<string>(actionTxQueryData["requestPledge"]);
            var tx = TxMarshaler.DeserializeUnsignedTx(ByteUtil.ParseHex(stake));
            Assert.Equal(publicKey, tx.PublicKey);
            Assert.Equal(publicKey.ToAddress(), tx.Signer);
            Assert.Equal(0, tx.Nonce);
            Assert.NotNull(tx.Timestamp);
            Assert.Equal(1, tx.GasLimit);
            Assert.Equal(1 * Currencies.Mead, tx.MaxGasPrice);
            var rawAction = Assert.Single(tx.Actions);
#pragma warning disable CS0612
            var action = new PolymorphicAction<ActionBase>();
#pragma warning restore CS0612
            action.LoadPlainValue(rawAction);
            var innerAction = Assert.IsType<RequestPledge>(action.InnerAction);
            Assert.Equal(address, innerAction.AgentAddress);
            Assert.Equal(RequestPledge.RefillMead, innerAction.Mead);
        }
    }
}
