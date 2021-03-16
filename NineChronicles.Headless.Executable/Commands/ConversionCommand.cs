using System;
using Cocona;
using Cocona.Help;
using Libplanet;
using Libplanet.Crypto;
using NineChronicles.Headless.Executable.IO;

namespace NineChronicles.Headless.Executable.Commands
{
    public class ConversionCommand
    {
        private readonly IConsole _console;

        public ConversionCommand(IConsole console)
        {
            _console = console;
        }

        public enum ConversionTarget
        {
            Address,
            PublicKey,
        }

        public void PrivateKey([Argument("PRIVATE-KEY")]string privateKeyHex, [Argument("TARGET-TYPE")] ConversionTarget to)
        {
            var privateKey = new PrivateKey(ByteUtil.ParseHex(privateKeyHex));
            string output = to switch
            {
                ConversionTarget.Address => privateKey.ToAddress().ToHex(),
                ConversionTarget.PublicKey => ByteUtil.Hex(privateKey.PublicKey.Format(true)),
                _ => throw new ArgumentOutOfRangeException(nameof(to)),
            };

            _console.Out.WriteLine(output);
        }

        [PrimaryCommand]
        public void Help([FromService] ICoconaHelpMessageBuilder helpMessageBuilder)
        {
            _console.Out.WriteLine(helpMessageBuilder.BuildAndRenderForCurrentContext());
        }
    }
}
