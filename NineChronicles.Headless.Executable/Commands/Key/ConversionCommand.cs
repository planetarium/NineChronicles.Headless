using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using NineChronicles.Headless.Executable.IO;

namespace NineChronicles.Headless.Executable.Commands.Key
{
    public class ConversionCommand
    {
        private readonly IConsole _console;

        public ConversionCommand(IConsole console)
        {
            _console = console;
        }

        public void PrivateKey(
            [Argument("PRIVATE-KEY")]
            string privateKeyHex,
            [Option]
            bool publicKey = false,
            [Option]
            bool address = false)
        {
            if (!(publicKey ^ address))
            {
                throw new CommandExitedException($"Only one flag should be used between {nameof(publicKey)} and {nameof(address)}", -1);
            }

            var privateKey = new PrivateKey(ByteUtil.ParseHex(privateKeyHex));
            if (address)
            {
                _console.Out.WriteLine(privateKey.Address.ToHex());
            }

            if (publicKey)
            {
                _console.Out.WriteLine(ByteUtil.Hex(privateKey.PublicKey.Format(true)));
            }
        }
    }
}
