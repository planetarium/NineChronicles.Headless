using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Libplanet.Crypto;
using Libplanet.Net;
using MagicOnion.Client;
using Nekoyume.Shared.Services;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests
{
    public class ProgramTest
    {
        private readonly string _apvString;
        private readonly string _genesisBlockPath;
        private readonly string _storePath;
        private readonly ushort _rpcPort;
        private readonly ushort _graphqlPort;

        public ProgramTest()
        {
            var privateKey = new PrivateKey();
            _apvString = AppProtocolVersion.Sign(privateKey, 1000).Token;

            _genesisBlockPath = "https://release.nine-chronicles.com/genesis-block-9c-main";
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            _rpcPort = 41234;
            _graphqlPort = 41238;
        }

        [Fact]
        public async Task Run()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            _ = new Program().Run(
                _apvString,
                _genesisBlockPath,
                noMiner: true,
                host: "localhost",
                rpcServer: true,
                rpcListenHost: "localhost",
                rpcListenPort: _rpcPort,
                graphQLServer: true,
                graphQLHost: "localhost",
                graphQLPort: _graphqlPort,
                storePath: _storePath,
                storeType: "rocksdb",
                skipPreload: true,
                noCors: true,
                cancellationToken: cancellationTokenSource.Token
            );

            try
            {
                // Wait until server start.
                // It can be flaky.
                await Task.Delay(10000).ConfigureAwait(false);

                using var client = new HttpClient();
                var queryString = "{\"query\":\"{chainQuery{blockQuery{block(index: 0) {hash}}}}\"}";
                var content = new StringContent(queryString);
                content.Headers.ContentLength = queryString.Length;
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync($"http://localhost:{_graphqlPort}/graphql", content);
                var responseString = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("\"data\":{\"chainQuery\":{\"blockQuery\":{\"block\":{\"hash\":\"4582250d0da33b06779a8475d283d5dd210c683b9b999d74d03fac4f58fa6bce\"}}}}", responseString);

                var channel = new Channel(
                    $"localhost:{_rpcPort}",
                    ChannelCredentials.Insecure,
                    new[]
                    {
                        new ChannelOption("grpc.max_receive_message_length", -1),
                        new ChannelOption("grpc.keepalive_permit_without_calls", 1),
                        new ChannelOption("grpc.keepalive_time_ms", 2000),
                    }
                );

                var service = MagicOnionClient.Create<IBlockChainService>(channel, Array.Empty<IClientFilter>())
                    .WithCancellationToken(channel.ShutdownToken);
                Assert.Equal(11085612, (await service.GetTip()).Length);
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }
        }
    }
}
