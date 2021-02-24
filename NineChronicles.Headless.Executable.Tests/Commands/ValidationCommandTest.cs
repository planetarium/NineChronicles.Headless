using NineChronicles.Headless.Executable.Commands;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ValidationCommandTest
    {
        private readonly ValidationCommand _command;

        public ValidationCommandTest()
        {
            _command = new ValidationCommand();
        }

        [Theory]
        [InlineData("", -1)]
        [InlineData("invalid hexadecimal", -1)]
        [InlineData("0000000000000000000000000000000000000000000000000000000000000000", -1)]
        [InlineData("ab8d591ccdcce263c39eb1f353e44b64869f0afea2df643bf6839ebde650d244", 0)]
        [InlineData("d6c3e0d525dac340a132ae05aaa9f3e278d61b70d2b71326570e64aee249e566", 0)]
        [InlineData("761f68d68426549df5904395b5ca5bce64a3da759085d8565242db42a5a1b0b9", 0)]
        public void PrivateKey(string privateKeyHex, int exitCode)
        {
            Assert.Equal(exitCode, _command.PrivateKey(privateKeyHex));
        }
    }
}
