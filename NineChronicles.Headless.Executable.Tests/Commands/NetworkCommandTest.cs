using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class NetworkCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly NetworkCommand _command;
        
        public NetworkCommandTest()
        {
            _console = new StringIOConsole();
            _command = new NetworkCommand(_console);
        }
        [Fact]
        public async Task APV()
        {
            Block<NCAction> genesis = BlockChain<NCAction>.MakeGenesisBlock();
            var blockChain = new BlockChain<NCAction>(
                new BlockPolicy<NCAction>(),
                new VolatileStagePolicy<NCAction>(),
                new DefaultStore(null),
                new TrieStateStore(
                    new DefaultKeyValueStore(null),
                    new DefaultKeyValueStore(null)
                ),
                genesis
            );
            var swarmKey = new PrivateKey();
            AppProtocolVersion apv = AppProtocolVersion.Sign(swarmKey, 1);
            using var swarm = new Swarm<NCAction>(
                blockChain, 
                swarmKey, 
                apv, 
                host: IPAddress.Loopback.ToString(), 
                listenPort: FreeTcpPort()
            );
            try
            {
                _ = swarm.StartAsync();
                await swarm.WaitForRunningAsync();
                var peerInfo = $"{ByteUtil.Hex(swarmKey.PublicKey.Format(true))},{swarm.EndPoint.Host},{swarm.EndPoint.Port}";
                _command.APV(peerInfo);
                Assert.Equal($"{apv.Token}", _console.Out.ToString().Trim());
            }
            finally
            {
                await swarm.StopAsync();
            }
        }

        private int FreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
