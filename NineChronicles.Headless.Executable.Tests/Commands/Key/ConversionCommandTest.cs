using Cocona;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Commands.Key;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ConversionCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly ConversionCommand _command;

        public ConversionCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ConversionCommand(_console);
        }

        [Theory]
        [InlineData("a1262b4f0911a9cec5e64344c0c9b50d64f8781ade0e09fa79faaa127ccdff89", true, false, "6BC0729733224139b16A4b1f18013C357d6be619\n")]
        [InlineData("a1262b4f0911a9cec5e64344c0c9b50d64f8781ade0e09fa79faaa127ccdff89", false, true, "02910a0274ece8408a0a166f9a4f0527ad60431b4480d1c96f2c0f26cfe528e994\n")]
        public void PrivateKey(string privateKeyHex, bool address, bool publicKey, string expectedOutput)
        {
            _command.PrivateKey(privateKeyHex, publicKey, address);
            Assert.Equal(expectedOutput, _console.Out.ToString());
        }

        [Fact]
        public void PrivateKey_AllowOnlyOneFlag()
        {
            Assert.Throws<CommandExitedException>(() => _command.PrivateKey("", true, true));
            Assert.Throws<CommandExitedException>(() => _command.PrivateKey("", false, false));
        }
    }
}
