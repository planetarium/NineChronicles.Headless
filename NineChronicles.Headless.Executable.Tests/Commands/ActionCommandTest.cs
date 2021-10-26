using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.Action;
using Nekoyume.BlockChain.Policy;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
using Serilog.Core;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;


namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ActionCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly ActionCommand _command;
        private readonly Codec _codec = new Codec();
        private readonly string _storePath;

        public ActionCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ActionCommand(_console);
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(true, -1)]
        [InlineData(false, 0)]
        public void ActivateAccount(bool invalid, int expectedCode)
        {
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState _) = ActivationKey.Create(privateKey, nonce);
            string invitationCode =  invalid ? "invalid_code" : activationKey.Encode();
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.ActivateAccount(invitationCode, ByteUtil.Hex(nonce), filePath);
            Assert.Equal(expectedCode, resultCode);

            if (resultCode == 0)
            {
                var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
                var decoded = (List)_codec.Decode(rawAction);
                string type = (Text)decoded[0];
                Assert.Equal(nameof(Nekoyume.Action.ActivateAccount), type);

                Dictionary plainValue = (Dictionary)decoded[1];
                var action = new ActivateAccount();
                action.LoadPlainValue(plainValue);
                Assert.Equal(activationKey.PrivateKey.Sign(nonce), action.Signature);
                Assert.Equal(activationKey.PendingAddress, action.PendingAddress);
            }
            else
            {
                Assert.Contains("hexWithSlash seems invalid. [invalid_code]", _console.Error.ToString());
            }
        }

        [Theory]
        [InlineData(10, 0, "transfer asset test1.")]
        [InlineData(100, 0, "transfer asset test2.")]
        [InlineData(1000, 0, null)]
        public async Task TransferAsset(
            int amount,
            int expectedCode,
            string? memo = null)
        {
            var genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(HashAlgorithmType.Of<SHA256>());
            IStore store = StoreType.RocksDb.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            const int minimumDifficulty = 5000000, maximumTransactions = 100;
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<NCAction>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy(
                minimumDifficulty,
                maximumTransactions);
            BlockChain<NCAction> blockChain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);
            var senderPrivateKey = new PrivateKey();
            var recipientPrivateKey = new PrivateKey();
            await blockChain.MineBlock(senderPrivateKey);
            store.Dispose();
            stateStore.Dispose();
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.TransferAsset(
                senderPrivateKey.ToAddress().ToHex(), 
                recipientPrivateKey.ToAddress().ToHex(),
                Convert.ToString(amount),
                filePath,
                memo);
            Assert.Equal(expectedCode, resultCode);

            if (resultCode == 0)
            {
                var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
                var decoded = (List)_codec.Decode(rawAction);
                string type = (Text)decoded[0];
                Assert.Equal(nameof(Nekoyume.Action.TransferAsset), type);
                var currency = new Currency("NCG", 2, minter: null);
                FungibleAssetValue amountFungibleAssetValue =
                    FungibleAssetValue.Parse(currency, Convert.ToString(amount));
                Dictionary plainValue = (Dictionary)decoded[1];
                var action = new TransferAsset();
                action.LoadPlainValue(plainValue);
                Assert.Equal(memo, action.Memo);
                Assert.Equal(amountFungibleAssetValue, action.Amount);
                Assert.Equal(senderPrivateKey.ToAddress(), action.Sender);
                Assert.Equal(recipientPrivateKey.ToAddress(), action.Recipient);
            }
            else
            {
                Assert.Contains("System.FormatException: Could not find any recognizable digits.", _console.Error.ToString());
            }
        }

        [Fact]
        public void MonsterCollect()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.MonsterCollect(1, filePath);
            Assert.Equal(0, resultCode);
            var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
            var decoded = (List)_codec.Decode(rawAction);
            string type = (Text)decoded[0];
            Assert.Equal(nameof(Nekoyume.Action.MonsterCollect), type);

            Dictionary plainValue = (Dictionary)decoded[1];
            var action = new MonsterCollect();
            action.LoadPlainValue(plainValue);
            Assert.Equal(1, action.level);
        }

        [Theory]
        [InlineData("0xab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d", -1)]
        [InlineData("ab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d", 0)]
        public void ClaimMonsterCollectReward(string addressString, int expectedCode)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.ClaimMonsterCollectionReward(addressString, filePath);
            Assert.Equal(expectedCode, resultCode);

            if (resultCode == 0)
            {
                var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
                var decoded = (List)_codec.Decode(rawAction);
                string type = (Text)decoded[0];
                Assert.Equal(nameof(Nekoyume.Action.ClaimMonsterCollectionReward), type);

                Dictionary plainValue = (Dictionary)decoded[1];
                var action = new ClaimMonsterCollectionReward();
                action.LoadPlainValue(plainValue);
                Assert.Equal(new Address(addressString), action.avatarAddress);
            }
            else
            {
                Assert.Contains("System.FormatException: Could not find any recognizable digits.", _console.Error.ToString());
            }
        }
    }
}
