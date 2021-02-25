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
                return -1;
            }
        }
    }
}
