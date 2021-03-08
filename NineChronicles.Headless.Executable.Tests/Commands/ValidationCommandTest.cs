using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ValidationCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly ValidationCommand _command;

        public ValidationCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ValidationCommand(_console);

            _console.SetNewLine("\n");
        }

        [Theory]
        [InlineData("", -1, "The given private key, '', had an issue during parsing.\n")]
        [InlineData("invalid hexadecimal", -1, "The given private key, 'invalid hexadecimal', had an issue during parsing.\n")]
        [InlineData("000000000000000000000000000000000000000000000000000000000000000000", -1, "The given private key, '000000000000000000000000000000000000000000000000000000000000000000', had an issue during parsing.\n")]
        [InlineData("ab8d591ccdcce263c39eb1f353e44b64869f0afea2df643bf6839ebde650d244", 0, "")]
        [InlineData("d6c3e0d525dac340a132ae05aaa9f3e278d61b70d2b71326570e64aee249e566", 0, "")]
        [InlineData("761f68d68426549df5904395b5ca5bce64a3da759085d8565242db42a5a1b0b9", 0, "")]
        public void PrivateKey(string privateKeyHex, int exitCode, string errorOutput)
        {
            Assert.Equal(exitCode, _command.PrivateKey(privateKeyHex));
            Assert.Equal(errorOutput, _console.Error.ToString());
        }
        
        [Theory]
        [InlineData("", -1, "The given public key, '', had an issue during parsing.\n")]
        [InlineData("invalid hexadecimal", -1, "The given public key, 'invalid hexadecimal', had an issue during parsing.\n")]
        [InlineData("000000000000000000000000000000000000000000000000000000000000000000", -1, "The given public key, '000000000000000000000000000000000000000000000000000000000000000000', had an issue during parsing.\n")]
        [InlineData("03b0868d9301b30c512d307ea67af4c8bef637ef099e39d32b808a43e6b41469c5", 0, "")]
        [InlineData("03308c1618a75e85a5fb57f7e453a642c307dc6310e90a7418b1aec565d963534a", 0, "")]
        [InlineData("028a6190bf643175b20e4a2d1d86fe6c4b8f7d5fe3d163632be4e59f83335824b8", 0, "")]
        public void PublicKey(string publicKeyHex, int exitCode, string errorOutput)
        {
            Assert.Equal(exitCode, _command.PublicKey(publicKeyHex));
            Assert.Equal(errorOutput, _console.Error.ToString());
        }
    }
}
