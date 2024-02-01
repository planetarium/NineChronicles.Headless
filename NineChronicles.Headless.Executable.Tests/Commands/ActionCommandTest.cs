using System;
using System.IO;
using Bencodex;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
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
        [InlineData(true, -1)]
        [InlineData(false, 0)]
        public void ActivateAccount(bool invalid, int expectedCode)
        {
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState _) = ActivationKey.Create(privateKey, nonce);
            string invitationCode = invalid ? "invalid_code" : activationKey.Encode();
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
        public void TransferAsset(
            int amount,
            int expectedCode,
            string? memo = null)
        {
            var senderPrivateKey = new PrivateKey();
            var recipientPrivateKey = new PrivateKey();
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.TransferAsset(
                senderPrivateKey.Address.ToHex(),
                recipientPrivateKey.Address.ToHex(),
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
                Assert.Equal(senderPrivateKey.Address, action.Sender);
                Assert.Equal(recipientPrivateKey.Address, action.Recipient);
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

        [Theory]
        [InlineData("0xab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d", -1)]
        [InlineData("ab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d", 0)]
        public void ClaimStakeReward(string addressString, int expectedCode)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.ClaimStakeReward(addressString, filePath);
            Assert.Equal(expectedCode, resultCode);

            if (resultCode == 0)
            {
                var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
                var decoded = (List)_codec.Decode(rawAction);
                string type = (Text)decoded[0];
                Assert.Equal(nameof(Nekoyume.Action.ClaimStakeReward), type);

                var plainValue = Assert.IsType<Dictionary>(decoded[1]);
                var action = new ClaimStakeReward();
                action.LoadPlainValue(plainValue);
            }
            else
            {
                Assert.Contains("System.FormatException: Input string was not in a correct format.", _console.Error.ToString());
            }
        }

        [Theory]
        [InlineData("0xab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d", -1)]
        [InlineData("ab1dce17dCE1Db1424BB833Af6cC087cd4F5CB6d", 0)]
        public void MigrateMonsterCollection(string addressString, int expectedCode)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var resultCode = _command.MigrateMonsterCollection(addressString, filePath);
            Assert.Equal(expectedCode, resultCode);

            if (resultCode == 0)
            {
                var rawAction = Convert.FromBase64String(File.ReadAllText(filePath));
                var decoded = (List)_codec.Decode(rawAction);
                string type = (Text)decoded[0];
                Assert.Equal(nameof(Nekoyume.Action.MigrateMonsterCollection), type);

                var plainValue = Assert.IsType<Dictionary>(decoded[1]);
                var action = new MigrateMonsterCollection();
                action.LoadPlainValue(plainValue);
                Assert.Equal(addressString, action.AvatarAddress.ToHex());
            }
            else
            {
                Assert.Contains("System.FormatException: Input string was not in a correct format.", _console.Error.ToString());
            }
        }
    }
}
