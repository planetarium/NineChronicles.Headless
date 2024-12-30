using System;
using System.IO;
using System.Linq;
using Lib9c;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class TxCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly TxCommand _command;
        private readonly PrivateKey _privateKey;
        private readonly Address _avatarAddress;
        private readonly BlockHash _blockHash;

        public TxCommandTest()
        {
            _console = new StringIOConsole();
            _command = new TxCommand(_console);
            _privateKey = new PrivateKey();
            _avatarAddress = new PrivateKey().Address;
            _blockHash = BlockHash.FromHashDigest(default);
        }

        [Theory]
        [InlineData(1, false)]
        [InlineData(10, true)]
        [InlineData(100, false)]
        public void Sign_TransferAsset(int amount, bool gas)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            actionCommand.TransferAsset(
                _privateKey.Address.ToHex(),
                new PrivateKey().Address.ToHex(),
                Convert.ToString(amount),
                filePath);
            Assert_Tx(1, filePath, gas);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Sign_Stake(bool gas)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            actionCommand.Stake(1, _avatarAddress, filePath);
            Assert_Tx(1, filePath, gas);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Sign_ClaimStakeReward(bool gas)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            var avatarAddress = new Address();
            actionCommand.ClaimStakeReward(
                avatarAddress.ToHex(),
                filePath);
            Assert_Tx(1, filePath, gas);
        }

        [Fact]
        public void Sign_MigrateMonsterCollection()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            var avatarAddress = new Address();
            actionCommand.MigrateMonsterCollection(avatarAddress.ToHex(), filePath);
            Assert_Tx(1, filePath, false);
        }

        private void Assert_Tx(long txNonce, string filePath, bool gas)
        {
            var timeStamp = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var hashHex = ByteUtil.Hex(_blockHash.ByteArray);
            long? maxGasPrice = gas ? (long?)1L : null;
            _command.Sign(ByteUtil.Hex(_privateKey.ByteArray), txNonce, hashHex, timeStamp.ToString(),
                new[] { filePath }, maxGasPrice: maxGasPrice);
            var output = _console.Out.ToString();
            var rawTx = Convert.FromBase64String(output!);
            var tx = Transaction.Deserialize(rawTx);
            Assert.Equal(txNonce, tx.Nonce);
            Assert.Equal(_blockHash, tx.GenesisHash);
            Assert.Equal(_privateKey.Address, tx.Signer);
            Assert.Equal(timeStamp, tx.Timestamp);
            ActionBase action = (ActionBase)new NCActionLoader().LoadAction(1L, tx.Actions.Single());
            long? expectedGasLimit = gas
                ? action is ITransferAsset || action is ITransferAssets
                    ? (long?)4L
                    : (long?)1L
                : null;

            Assert.Equal(expectedGasLimit, tx.GasLimit);
            if (gas)
            {
                Assert.Equal(1 * Currencies.Mead, tx.MaxGasPrice);
            }
            else
            {
                Assert.Null(tx.MaxGasPrice);
            }
        }
    }
}
