using System.IO;
using System.Net;
using Bencodex;
using Bencodex.Types;
using Libplanet;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;


namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class TxCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly TxCommand _command;
        private readonly Codec _codec = new Codec();

        public TxCommandTest()
        {
            _console = new StringIOConsole();
            _command = new TxCommand(_console);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void Sign(int txNonce, bool toBytes)
        {
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState _) =
                ActivationKey.Create(privateKey, nonce);
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var actionCommand = new ActionCommand(_console);
            actionCommand.ActivateAccount(activationKey.Encode(), ByteUtil.Hex(nonce), filePath);
            var blockHash = BlockHash.FromHashDigest(default);
            var hashHex = ByteUtil.Hex(blockHash.ByteArray);
            _command.Sign(ByteUtil.Hex(privateKey.ByteArray), txNonce, hashHex, new[] { filePath }, bytes: toBytes);
            var output = _console.Out.ToString();
            if (!toBytes)
            {
                // trim new lines.
                output = output.Trim();
            }
            var rawTx = ByteUtil.ParseHex(output);
            var tx = Transaction<NCAction>.Deserialize(rawTx);
            Assert.Equal(txNonce, tx.Nonce);
            Assert.Equal(blockHash, tx.GenesisHash);
            Assert.Equal(privateKey.ToAddress(), tx.Signer);
        }
    }
}
