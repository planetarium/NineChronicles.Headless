using System;
using System.IO;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Nekoyume.Action;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;


namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ActionCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly ActionCommand _command;
        private readonly Codec _codec = new Codec();

        public ActionCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ActionCommand(_console);
        }

        [Theory]
        [InlineData(10, 0, "transfer asset test1.")]
        [InlineData(100, 0, "transfer asset test2.")]
        [InlineData(1000, 0, null)]
        public void TransferAsset(
            int amount,
            int expectedCode,
            string? memo = null)
        {
            var senderPrivateKey = new PrivateKey();
            var recipientPrivateKey = new PrivateKey();
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
                Dictionary plainValue = (Dictionary)decoded[1];
                var action = new TransferAsset();
                action.LoadPlainValue(plainValue);
                Assert.Equal(memo, action.Memo);
                Assert.Equal(amount, action.Amount.MajorUnit);
                Assert.Equal(senderPrivateKey.ToAddress(), action.Sender);
                Assert.Equal(recipientPrivateKey.ToAddress(), action.Recipient);
            }
            else
            {
                Assert.Contains("System.FormatException: Input string was not in a correct format.", _console.Error.ToString());
            }
        }

        [Fact]
        public void Stake()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.Stake(1, filePath);
            Assert.Equal(0, resultCode);
            var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
            var decoded = (List)_codec.Decode(rawAction);
            string type = (Text)decoded[0];
            Assert.Equal(nameof(Nekoyume.Action.Stake), type);

            var plainValue = Assert.IsType<Dictionary>(decoded[1]);
            var action = new Stake();
            action.LoadPlainValue(plainValue);
        }
    }
}
