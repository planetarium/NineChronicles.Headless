using System;
using System.IO;
using System.Net;
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

        public ProgramTest()
        {
            var privateKey = new PrivateKey();
            _apvString = AppProtocolVersion.Sign(privateKey, 1000).Token;

            _genesisBlockPath = "https://9c-test.s3.ap-northeast-2.amazonaws.com/genesis-block-9c-main";
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
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
                rpcListenPort: 31234,
                graphQLServer: true,
                graphQLHost: "localhost",
                graphQLPort: 5000,
                storePath: _storePath,
                storeType: "rocksdb",
                cancellationToken: cancellationTokenSource.Token);

            try
            {
                // Wait until server start.
                // It can be flaky.
                await Task.Delay(10000).ConfigureAwait(false);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:5000/graphql");
                request.Method = "POST";
                var requestStream = request.GetRequestStream();
                var content = "{\"query\":\"{chainQuery{blockQuery{block(index: 0) {hash}}}}\"}";
                request.ContentLength = content.Length;
                request.ContentType = "application/json";
                requestStream.Write(Encoding.UTF8.GetBytes(content));
                requestStream.Close();
                var response = request.GetResponse();
                var responseStream = response.GetResponseStream();
                var streamReader = new StreamReader(responseStream);
                var responseString = streamReader.ReadToEnd();

                Assert.Contains("\"data\":{\"chainQuery\":{\"blockQuery\":{\"block\":{\"hash\":\"4582250d0da33b06779a8475d283d5dd210c683b9b999d74d03fac4f58fa6bce\"}}}}", responseString);
                
                var channel = new Channel(
                    "localhost:31234",
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
                Assert.Equal(11085640, (await service.GetTip()).Length);
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }
        }
    }
}
