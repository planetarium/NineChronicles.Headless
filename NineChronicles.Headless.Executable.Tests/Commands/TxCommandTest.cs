using System;
using System.IO;
using Bencodex;
using Libplanet;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;


namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class TxCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly TxCommand _command;
        private readonly PrivateKey _privateKey;
        private readonly BlockHash _blockHash;

        public TxCommandTest()
        {
            _console = new StringIOConsole();
            _command = new TxCommand(_console);
            _privateKey = new PrivateKey();
            _blockHash = BlockHash.FromHashDigest(default);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void Sign_ActivateAccount(int txNonce)
        {
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            (ActivationKey activationKey, PendingActivationState _) =
                ActivationKey.Create(_privateKey, nonce);
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            actionCommand.ActivateAccount(activationKey.Encode(), ByteUtil.Hex(nonce), filePath);
            Assert_Tx(txNonce, filePath);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Sign_TransferAsset(int amount)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            actionCommand.TransferAsset(
                _privateKey.ToAddress().ToHex(),
                new PrivateKey().ToAddress().ToHex(),
                Convert.ToString(amount),
                filePath);
            Assert_Tx(1, filePath);
        }

        [Fact]
        public void Sign_MonsterCollect()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            actionCommand.MonsterCollect(1, filePath);
            Assert_Tx(1, filePath);
        }

        [Fact]
        public void Sign_ClaimMonsterCollectionReward()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            var avatarAddress = new Address();
            actionCommand.ClaimMonsterCollectionReward(avatarAddress.ToHex(), filePath);
            Assert_Tx(1, filePath);
        }

        private void Assert_Tx(long txNonce, string filePath)
        {
            var timeStamp = default(DateTimeOffset);
            var hashHex = ByteUtil.Hex(_blockHash.ByteArray);
            _command.Sign(ByteUtil.Hex(_privateKey.ByteArray), txNonce, hashHex, timeStamp.ToString(),
                new[] { filePath });
            var output = _console.Out.ToString();
            var rawTx = Convert.FromBase64String(output!);
            var tx = Transaction<NCAction>.Deserialize(rawTx);
            Assert.Equal(txNonce, tx.Nonce);
            Assert.Equal(_blockHash, tx.GenesisHash);
            Assert.Equal(_privateKey.ToAddress(), tx.Signer);
            Assert.Equal(timeStamp, tx.Timestamp);
        }
    }
}
