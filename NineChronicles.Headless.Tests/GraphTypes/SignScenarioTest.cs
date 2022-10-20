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
    public class SignScenarioTest : GraphQLTestBase
    {
        public SignScenarioTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task SignTransaction_TransferAsset()
        {
            var privateKey = new PrivateKey();
            var privateKey2 = new PrivateKey();
            var recipient = privateKey.ToAddress();
            var sender = privateKey2.ToAddress();

            // Create Action.
            var args = $"recipient: \"{recipient}\", sender: \"{sender}\", amount: \"17.5\", currency: CRYSTAL";
            object plainValue = await GetAction("transferAsset", args);

            (Transaction<NCAction> signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue);
            var action = Assert.IsType<TransferAsset>(signedTx.CustomActions!.Single().InnerAction);
            Assert.Equal(recipient, action.Recipient);
            Assert.Equal(sender, action.Sender);
            Assert.Equal(FungibleAssetValue.Parse(CrystalCalculator.CRYSTAL, "17.5"), action.Amount);
            await StageTransaction(signedTx, hex);
        }

        [Fact]
        public async Task SignTransaction_Raid()
        {
            var privateKey = new PrivateKey();
            var avatarAddress = privateKey.ToAddress();
            var guid = Guid.NewGuid();
            string ids = $"[\"{guid}\"]";

            // Create Action.
            var args = $"avatarAddress: \"{avatarAddress}\", equipmentIds: {ids}, costumeIds: {ids}, foodIds: {ids}, payNcg: true";
            object plainValue = await GetAction("raid", args);

            (Transaction<NCAction> signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue);
            var action = Assert.IsType<Raid>(signedTx.CustomActions!.Single().InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Guid equipmentId = Assert.Single(action.EquipmentIds);
            Guid costumeId = Assert.Single(action.CostumeIds);
            Guid foodId = Assert.Single(action.FoodIds);
            Assert.All(new[] { equipmentId, costumeId, foodId }, id => Assert.Equal(guid, id));
            Assert.True(action.PayNcg);
            await StageTransaction(signedTx, hex);
        }

        [Fact]
        public async Task SignTransaction_ClaimRaidReward()
        {
            var privateKey = new PrivateKey();
            var avatarAddress = privateKey.ToAddress();
            // Create Action.
            var args = $"avatarAddress: \"{avatarAddress}\"";
            object plainValue = await GetAction("claimRaidReward", args);

            (Transaction<NCAction> signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue);
            var action = Assert.IsType<ClaimRaidReward>(signedTx.CustomActions!.Single().InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            await StageTransaction(signedTx, hex);
        }

        [Fact]
        public async Task SignTransaction_ClaimWorldBossKillReward()
        {
            var privateKey = new PrivateKey();
            var avatarAddress = privateKey.ToAddress();
            // Create Action.
            var args = $"avatarAddress: \"{avatarAddress}\"";
            object plainValue = await GetAction("claimWorldBossKillReward", args);

            (Transaction<NCAction> signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue);
            var action = Assert.IsType<ClaimWordBossKillReward>(signedTx.CustomActions!.Single().InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            await StageTransaction(signedTx, hex);
        }

        [Fact]
        public async Task SignTransaction_PrepareRewardAssets()
        {
            var privateKey = new PrivateKey();
            var rewardPoolAddress = privateKey.ToAddress();
            // Create Action.
            var args = $"rewardPoolAddress: \"{rewardPoolAddress}\", assets:[{{ quantity: 100, decimalPlaces: 0, ticker: \"CRYSTAL\" }}]";
            object plainValue = await GetAction("prepareRewardAssets", args);

            (Transaction<NCAction> signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue);
            var action = Assert.IsType<PrepareRewardAssets>(signedTx.CustomActions!.Single().InnerAction);
            Assert.Equal(rewardPoolAddress, action.RewardPoolAddress);
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            Assert.Equal(Currency.Legacy("CRYSTAL", 0, null) * 100, action.Assets.Single());
#pragma warning restore CS0618
            await StageTransaction(signedTx, hex);
        }

        private async Task<object> GetAction(string actionName, string queryArgs)
        {
            var actionQuery = $"{{ {actionName}({queryArgs}) }}";
            var actionQueryResult = await ExecuteQueryAsync<ActionQuery>(actionQuery, standaloneContext: StandaloneContextFx);
            var actionData = (Dictionary<string, object>)((ExecutionNode)actionQueryResult.Data!).ToValue()!;
            return actionData[actionName];
        }

        private async Task<(Transaction<NCAction>, string)> GetSignedTransaction(PrivateKey privateKey, object plainValue)
        {
            // Get Nonce.
            var nonceQuery = $@"query {{
                    nextTxNonce(address: ""{privateKey.ToAddress()}"")
                }}";
            var nonceQueryResult =
                await ExecuteQueryAsync<TransactionHeadlessQuery>(nonceQuery, standaloneContext: StandaloneContextFx);
            var nonce =
                (long)((Dictionary<string, object>)((ExecutionNode)nonceQueryResult.Data!)
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
                (string)((Dictionary<string, object>)((ExecutionNode)unsignedQueryResult.Data!).ToValue()!)[
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
            var hex = (string)((Dictionary<string, object>)((ExecutionNode)signQueryResult.Data!).ToValue()!)[
                "signTransaction"];
            byte[] result = ByteUtil.ParseHex(hex);
            Transaction<NCAction> signedTx = Transaction<NCAction>.Deserialize(result);

            Assert.Equal(unsignedTx.PublicKey, signedTx.PublicKey);
            Assert.Equal(unsignedTx.Signer, signedTx.Signer);
            Assert.Equal(nonce, signedTx.Nonce);
            Assert.Equal(unsignedTx.UpdatedAddresses, signedTx.UpdatedAddresses);
            Assert.Equal(unsignedTx.Timestamp, signedTx.Timestamp);
            Assert.Single(unsignedTx.CustomActions!);
            Assert.Single(signedTx.CustomActions!);
            return (signedTx, hex);
        }

        private async Task StageTransaction(Transaction<NCAction> signedTx, string hex)
        {
            // Staging Transaction.
            var stageTxMutation = $"mutation {{ stageTransaction(payload: \"{hex}\") }}";
            var stageTxResult = await ExecuteQueryAsync(stageTxMutation);
            var txId =
                (string)((Dictionary<string, object>)((ExecutionNode)stageTxResult.Data!).ToValue()!)["stageTransaction"];
            Assert.Equal(signedTx.Id.ToHex(), txId);
            Assert.Contains(signedTx.Id, StandaloneContextFx.BlockChain!.GetStagedTransactionIds());
        }
    }
}
