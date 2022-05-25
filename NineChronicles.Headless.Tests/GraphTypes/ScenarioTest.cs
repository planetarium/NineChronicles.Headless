using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona;
using Libplanet.KeyStore;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Helper;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using NineChronicles.Headless.Executable.Tests.KeyStore;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using Xunit.Abstractions;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;


namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class ScenarioTest : GraphQLTestBase
    {
        public ScenarioTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task SignTransaction()
        {
            var privateKey = new PrivateKey();
            var privateKey2 = new PrivateKey();
            var recipient = privateKey.ToAddress();
            var sender = privateKey2.ToAddress();

            // Create Action.
            var args = $"recipient: \"{recipient}\", sender: \"{sender}\", amount: \"17.5\", currency: CRYSTAL";
            var actionQuery = $"{{ transferAsset({args}) }}";
            var actionQueryResult = await ExecuteQueryAsync<ActionQuery>(actionQuery, standaloneContext: StandaloneContextFx);
            var actionData = (Dictionary<string, object>) ((ExecutionNode) actionQueryResult.Data!).ToValue()!;
            var plainValue = actionData["transferAsset"];

            // Get Nonce.
            var nonceQuery = $@"query {{
                    nextTxNonce(address: ""{privateKey.ToAddress()}"")
                }}";
            var nonceQueryResult =
                await ExecuteQueryAsync<TransactionHeadlessQuery>(nonceQuery, standaloneContext: StandaloneContextFx);
            var nonce =
                (long) ((Dictionary<string, object>) ((ExecutionNode) nonceQueryResult.Data!)
                    .ToValue()!)["nextTxNonce"];

            // Get PublicKey.
            var keyStore = new InMemoryKeyStore();
            Guid keyId = keyStore.Add(ProtectedPrivateKey.Protect(privateKey, "1234"));
            var passPhrase = new PassphraseParameters();
            passPhrase.Passphrase = "1234";
            var console = new StringIOConsole();
            var keyCommand = new KeyCommand(console, keyStore);
            keyCommand.PublicKey(keyId, passPhrase);
            var hexedPublicKey = console.Out.ToString().Trim();

            Assert.Equal(hexedPublicKey, ByteUtil.Hex(privateKey.PublicKey.Format(false)));

            // Create unsigned Transaction.
            var unsignedQuery = $@"query {{
                    unsignedTransaction(publicKey: ""{hexedPublicKey}"", plainValue: ""{plainValue}"", nonce: {nonce})
                }}";
            var unsignedQueryResult =
                await ExecuteQueryAsync<TransactionHeadlessQuery>(unsignedQuery, standaloneContext: StandaloneContextFx);
            var unsignedData =
                (string) ((Dictionary<string, object>) ((ExecutionNode) unsignedQueryResult.Data!).ToValue()!)[
                    "unsignedTransaction"];
            var unsignedTxBytes = ByteUtil.ParseHex(unsignedData);
            Transaction<NCAction> unsignedTx = Transaction<NCAction>.Deserialize(unsignedTxBytes, false);

            // Sign Transaction in local.
            var path = Path.GetTempFileName();
            await File.WriteAllBytesAsync(path, unsignedTxBytes);
            var outputPath = Path.GetTempFileName();
            keyCommand.Sign(keyId, path, passPhrase, outputPath);

            var signature = ByteUtil.Hex(await File.ReadAllBytesAsync(outputPath));
            Assert.Equal(ByteUtil.Hex(privateKey.Sign(unsignedTxBytes)), signature);

            // Attachment signature & unsigned transaction
            var signQuery = $@"query {{
                    signTransaction(unsignedTransaction: ""{unsignedData}"", signature: ""{signature}"")
                }}";
            var signQueryResult =
                await ExecuteQueryAsync<TransactionHeadlessQuery>(signQuery, standaloneContext: StandaloneContextFx);
            var hex = (string) ((Dictionary<string, object>) ((ExecutionNode) signQueryResult.Data!).ToValue()!)[
                "signTransaction"];
            byte[] result = ByteUtil.ParseHex(hex);
            Transaction<NCAction> signedTx = Transaction<NCAction>.Deserialize(result);

            Assert.Equal(unsignedTx.PublicKey, signedTx.PublicKey);
            Assert.Equal(unsignedTx.Signer, signedTx.Signer);
            Assert.Equal(nonce, signedTx.Nonce);
            Assert.Equal(unsignedTx.UpdatedAddresses, signedTx.UpdatedAddresses);
            Assert.Equal(unsignedTx.Timestamp, signedTx.Timestamp);
            Assert.Single(unsignedTx.Actions);
            Assert.Single(signedTx.Actions);
            Assert.IsType<TransferAsset>(signedTx.Actions.Single().InnerAction);
            var action = Assert.IsType<TransferAsset>(signedTx.Actions.Single().InnerAction);
            Assert.Equal(recipient, action.Recipient);
            Assert.Equal(sender, action.Sender);
            Assert.Equal(FungibleAssetValue.Parse(CrystalCalculator.CRYSTAL, "17.5"), action.Amount);

            // Staging Transaction.
            var stageTxMutation = $"mutation {{ stageTransaction(payload: \"{hex}\") }}";
            var stageTxResult = await ExecuteQueryAsync(stageTxMutation);
            var txId =
                (string) ((Dictionary<string, object>) ((ExecutionNode) stageTxResult.Data!).ToValue()!)["stageTransaction"];
            Assert.Equal(signedTx.Id.ToHex(), txId);
            Assert.Contains(signedTx.Id, StandaloneContextFx.BlockChain!.GetStagedTransactionIds());
        }
    }
}
