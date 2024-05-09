using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using MagicOnion.Client;
using Nekoyume.Action.Loader;
using Nekoyume.Blockchain.Policy;
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
        private readonly string _genesisBlockHash;
        private readonly byte[] _genesisEncoded;

        public ProgramTest()
        {
            var privateKey = new PrivateKey();
            _apvString = AppProtocolVersion.Sign(privateKey, 1000).Token;

            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_storePath);
            _genesisBlockPath = Path.Combine(_storePath, "./genesis");
            var genesis = BlockChain.ProposeGenesisBlock();

            Bencodex.Codec codec = new Bencodex.Codec();
            _genesisBlockHash = ByteUtil.Hex(genesis.Hash.ByteArray);
            _genesisEncoded = codec.Encode(BlockMarshaler.MarshalBlock(genesis));
            File.WriteAllBytes(_genesisBlockPath, _genesisEncoded);

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
                Assert.Contains($"\"data\":{{\"chainQuery\":{{\"blockQuery\":{{\"block\":{{\"hash\":\"{_genesisBlockHash}\"}}}}}}}}", responseString);

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
                Assert.Equal(_genesisEncoded.Length, (await service.GetTip()).Length);
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }
        }
    }
}
