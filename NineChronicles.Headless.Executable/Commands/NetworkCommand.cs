using System;
using Cocona;
using Cocona.Help;
using Libplanet.Extensions.Cocona;
using Libplanet.Headless;
using Libplanet.Net;
using Libplanet.Net.Transports;
using NineChronicles.Headless.Executable.IO;

namespace NineChronicles.Headless.Executable.Commands
{
    public class NetworkCommand : CoconaLiteConsoleAppBase
    {
        private readonly IConsole _console;

        public NetworkCommand(IConsole console)
        {
            _console = console;
        }
        
        [PrimaryCommand]
        public void Help([FromService] ICoconaHelpMessageBuilder helpMessageBuilder)
        {
            _console.Out.WriteLine(helpMessageBuilder.BuildAndRenderForCurrentContext());
        }

        [Command(Description = "Query app protocol version (a.k.a. APV) of target node.")]
        public void APV(
            [Argument(
                Name = "target",
                Description = "Comma seperated peer information of target node.")]
            string peerInfo)
        {
            try
            {
                BoundPeer peer = PropertyParser.ParsePeer(peerInfo);
                _console.Out.WriteLine(peer.QueryAppProtocolVersion().Token);
            }
            catch (Exception e)
            {
                throw Utils.Error($"Unexpected error occurred. [{e}]");
            }
        }
    }
}
