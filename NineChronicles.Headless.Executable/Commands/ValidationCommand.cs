using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using NineChronicles.Headless.Executable.IO;

namespace NineChronicles.Headless.Executable.Commands
{
    public class ValidationCommand : CoconaLiteConsoleAppBase
    {
        private readonly IConsole _console;

        public ValidationCommand(IConsole console)
        {
            _console = console;
        }

        [Command(Description = "Validate private key")]
        public int PrivateKey(
            [Argument(
                Name = "PRIVATE-KEY",
                Description = "A hexadecimal representation of private key to validate.")]
            string privateKeyHex)
        {
            try
            {
                _ = new PrivateKey(ByteUtil.ParseHex(privateKeyHex));
                return 0;
            }
            catch
            {
                _console.Error.WriteLine($"The given private key had an issue during parsing.");
                return -1;
            }
        }

        [Command(Description = "Validate public key")]
        public int PublicKey(
            [Argument(
                Name = "PUBLIC-KEY",
                Description = "A hexadecimal representation of public key to validate.")]
            string publicKeyHex)
        {
            try
            {
                _ = new PublicKey(ByteUtil.ParseHex(publicKeyHex));
                return 0;
            }
            catch
            {
                _console.Error.WriteLine($"The given public key, '{publicKeyHex}', had an issue during parsing.");
                return -1;
            }
        }
    }
}
