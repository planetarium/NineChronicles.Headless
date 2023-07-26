using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Lib9c;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona;
using Libplanet.KeyStore;
using Libplanet.Types.Assets;
using Libplanet.Types.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Helper;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using NineChronicles.Headless.Executable.Tests.KeyStore;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using Xunit.Abstractions;
using static NineChronicles.Headless.NCActionUtils;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class SignScenarioTest : GraphQLTestBase
    {
        public SignScenarioTest(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignTransaction_TransferAsset(bool gas)
        {
            var privateKey = new PrivateKey();
            var privateKey2 = new PrivateKey();
            var recipient = privateKey.ToAddress();
            var sender = privateKey2.ToAddress();

            // Create Action.
            var args = $"recipient: \"{recipient}\", sender: \"{sender}\", amount: \"17.5\", currency: CRYSTAL";
            object plainValue = await GetAction("transferAsset", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, gas);
            var action = Assert.IsType<TransferAsset>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(recipient, action.Recipient);
            Assert.Equal(sender, action.Sender);
            Assert.Equal(FungibleAssetValue.Parse(CrystalCalculator.CRYSTAL, "17.5"), action.Amount);
            await StageTransaction(signedTx, hex);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignTransaction_Raid(bool gas)
        {
            var privateKey = new PrivateKey();
            var avatarAddress = privateKey.ToAddress();
            var guid = Guid.NewGuid();
            string ids = $"[\"{guid}\"]";

            // Create Action.
            var args = $"avatarAddress: \"{avatarAddress}\", equipmentIds: {ids}, costumeIds: {ids}, foodIds: {ids}, payNcg: true, runeSlotInfos: [{{ slotIndex: 1, runeId: 2 }}]";
            object plainValue = await GetAction("raid", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, gas);
            var action = Assert.IsType<Raid>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Guid equipmentId = Assert.Single(action.EquipmentIds);
            Guid costumeId = Assert.Single(action.CostumeIds);
            Guid foodId = Assert.Single(action.FoodIds);
            Assert.All(new[] { equipmentId, costumeId, foodId }, id => Assert.Equal(guid, id));
            Assert.True(action.PayNcg);
            Assert.Single(action.RuneInfos);
            await StageTransaction(signedTx, hex);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignTransaction_ClaimRaidReward(bool gas)
        {
            var privateKey = new PrivateKey();
            var avatarAddress = privateKey.ToAddress();
            // Create Action.
            var args = $"avatarAddress: \"{avatarAddress}\"";
            object plainValue = await GetAction("claimRaidReward", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, gas);
            var action = Assert.IsType<ClaimRaidReward>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(avatarAddress, action.AvatarAddress);
            await StageTransaction(signedTx, hex);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignTransaction_ClaimWorldBossKillReward(bool gas)
        {
            var privateKey = new PrivateKey();
            var avatarAddress = privateKey.ToAddress();
            // Create Action.
            var args = $"avatarAddress: \"{avatarAddress}\"";
            object plainValue = await GetAction("claimWorldBossKillReward", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, gas);
            var action = Assert.IsType<ClaimWordBossKillReward>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(avatarAddress, action.AvatarAddress);
            await StageTransaction(signedTx, hex);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignTransaction_PrepareRewardAssets(bool gas)
        {
            var privateKey = new PrivateKey();
            var rewardPoolAddress = privateKey.ToAddress();
            // Create Action.
            var args = $"rewardPoolAddress: \"{rewardPoolAddress}\", assets:[{{ quantity: 100, decimalPlaces: 0, ticker: \"CRYSTAL\" }}]";
            object plainValue = await GetAction("prepareRewardAssets", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, gas);
            var action = Assert.IsType<PrepareRewardAssets>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(rewardPoolAddress, action.RewardPoolAddress);
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            Assert.Equal(Currency.Legacy("CRYSTAL", 0, null) * 100, action.Assets.Single());
#pragma warning restore CS0618
            await StageTransaction(signedTx, hex);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SignTransaction_TransferAssets(bool gas)
        {
            var privateKey = new PrivateKey();
            var sender = privateKey.ToAddress();
            // Create Action.
            var args = $"sender: \"{sender}\", recipients: [{{ recipient: \"{sender}\", amount: {{ quantity: 100, decimalPlaces: 18, ticker: \"CRYSTAL\" }} }}, {{ recipient: \"{sender}\", amount: {{ quantity: 100, decimalPlaces: 0, ticker: \"RUNE_FENRIR1\" }} }}]";
            object plainValue = await GetAction("transferAssets", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, gas);
            var action = Assert.IsType<TransferAssets>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(sender, action.Sender);
            Assert.Equal(2, action.Recipients.Count);
            await StageTransaction(signedTx, hex);
        }

        [Fact]
        public async Task SignTransaction_CreatePledge()
        {
            var privateKey = new PrivateKey();
            var sender = privateKey.ToAddress();
            // Create Action.
            var args = $"patronAddress: \"{MeadConfig.PatronAddress}\", agentAddresses: [\"{sender}\"]";
            object plainValue = await GetAction("createPledge", args);

            (Transaction signedTx, string hex) = await GetSignedTransaction(privateKey, plainValue, true);
            var action = Assert.IsType<CreatePledge>(ToAction(signedTx.Actions!.Single()));
            Assert.Equal(sender, action.AgentAddresses.Single().Item1);
            Assert.Equal(MeadConfig.PatronAddress, action.PatronAddress);
            Assert.Equal(1, signedTx.GasLimit);
            Assert.Equal(1 * Currencies.Mead, signedTx.MaxGasPrice);
            await StageTransaction(signedTx, hex);
        }

        private async Task<object> GetAction(string actionName, string queryArgs)
        {
            var actionQuery = $"{{ {actionName}({queryArgs}) }}";
            var actionQueryResult = await ExecuteQueryAsync<ActionQuery>(actionQuery, standaloneContext: StandaloneContextFx);
            var actionData = (Dictionary<string, object>)((ExecutionNode)actionQueryResult.Data!).ToValue()!;
            return actionData[actionName];
        }

        private async Task<(Transaction, string)> GetSignedTransaction(PrivateKey privateKey, object plainValue, bool gas)
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
            var unsignedQuery = gas ? $@"query {{
                    unsignedTransaction(publicKey: ""{hexedPublicKey}"", plainValue: ""{plainValue}"", nonce: {nonce}, maxGasPrice: {{ quantity: 1, decimalPlaces: 18, ticker: ""Mead"" }})
                }}" : $@"query {{
                    unsignedTransaction(publicKey: ""{hexedPublicKey}"", plainValue: ""{plainValue}"", nonce: {nonce})
                }}";
            var unsignedQueryResult =
                await ExecuteQueryAsync<TransactionHeadlessQuery>(unsignedQuery, standaloneContext: StandaloneContextFx);
            var unsignedData =
                (string)((Dictionary<string, object>)((ExecutionNode)unsignedQueryResult.Data!).ToValue()!)[
                    "unsignedTransaction"];
            var unsignedTxBytes = ByteUtil.ParseHex(unsignedData);
            IUnsignedTx unsignedTx = TxMarshaler.DeserializeUnsignedTx(unsignedTxBytes);

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
            Transaction signedTx = Transaction.Deserialize(result);
            IValue rawAction = signedTx.Actions.Single();
            ActionBase action = (ActionBase)new NCActionLoader().LoadAction(1L, rawAction);
            long expectedGasLimit = action is ITransferAsset or ITransferAssets ? 4 : 1;

            Assert.Equal(unsignedTx.PublicKey, signedTx.PublicKey);
            Assert.Equal(unsignedTx.Signer, signedTx.Signer);
            Assert.Equal(nonce, signedTx.Nonce);
            Assert.Equal(unsignedTx.UpdatedAddresses, signedTx.UpdatedAddresses);
            Assert.Equal(unsignedTx.Timestamp, signedTx.Timestamp);
            Assert.Single(unsignedTx.Actions);
            Assert.Single(signedTx.Actions!);
            Assert.Equal(expectedGasLimit, signedTx.GasLimit);
            Assert.Equal(1 * Currencies.Mead, signedTx.MaxGasPrice);
            return (signedTx, hex);
        }

        private async Task StageTransaction(Transaction signedTx, string hex)
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
