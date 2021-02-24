using Cocona;
using Libplanet;
using Libplanet.Crypto;

namespace NineChronicles.Headless.Executable.Commands
{
    public class ValidationCommand : CoconaLiteConsoleAppBase
    {
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
                return -1;
            }
        }
    }
}
