using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Claims;
using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.Controllers;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Properties;
using NineChronicles.Headless.Requests;
using Xunit;
using IPAddress = System.Net.IPAddress;

namespace NineChronicles.Headless.Tests.Controllers
{
    public class GraphQLControllerTest
    {
        private readonly GraphQLController _controller;
        private readonly StandaloneContext _standaloneContext;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GraphQLControllerTest()
        {
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var genesisBlock = BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>()
            );
            var blockchain = new BlockChain<PolymorphicAction<ActionBase>>(
                new BlockPolicy<PolymorphicAction<ActionBase>>(),
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
                store,
                stateStore,
                genesisBlock);
            _standaloneContext = new StandaloneContext
            {
                BlockChain = blockchain,
                Store = store,
            };
            _configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            _httpContextAccessor = new HttpContextAccessor();
            _httpContextAccessor.HttpContext = new DefaultHttpContext();
            ConfigureNineChroniclesNodeService();
            _controller = new GraphQLController(_standaloneContext, _httpContextAccessor, _configuration);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SetPrivateKey(bool useSecretToken)
        {
            if (useSecretToken)
            {
                ConfigureSecretToken();
                ConfigureAdminClaim();
            }

            ConfigureNineChroniclesNodeService();
            var privateKey = new PrivateKey();
            Assert.IsType<OkObjectResult>(_controller.SetPrivateKey(new SetPrivateKeyRequest
            {
                PrivateKeyString = ByteUtil.Hex(privateKey.ByteArray),
            }));

            Assert.Equal(_standaloneContext.NineChroniclesNodeService!.MinerPrivateKey, privateKey);
        }

        [Fact]
        public void SetPrivateKeyThrowsConflict()
        {
            _standaloneContext.NineChroniclesNodeService = null;
            IActionResult result = _controller.SetPrivateKey(new SetPrivateKeyRequest());
            Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, ((StatusCodeResult)result).StatusCode);
        }

        [Fact]
        public void SetPrivateKeyThrowsUnauthorizedIfSecretTokenUsed()
        {
            ConfigureSecretToken();
            Assert.IsType<UnauthorizedResult>(_controller.SetPrivateKey(new SetPrivateKeyRequest()));
        }

        [Fact]
        public void SetPrivateKeyThrowsBadRequest()
        {
            ConfigureNineChroniclesNodeService();
            var privateKey = new PrivateKey();
            IActionResult result = _controller.SetPrivateKey(new SetPrivateKeyRequest
            {
                PrivateKeyString = "test",
            });
            Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, ((StatusCodeResult)result).StatusCode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RemoveSubscribe(bool exist)
        {
            var address = new PrivateKey().ToAddress();
            if (exist)
            {
                _standaloneContext.AgentAddresses[address] = (new ReplaySubject<MonsterCollectionStatus>(), new ReplaySubject<MonsterCollectionState>(), new ReplaySubject<string>());
            }
            Assert.Equal(exist, _standaloneContext.AgentAddresses.Any());
            _controller.RemoveSubscribe(new AddressRequest
            {
                AddressString = address.ToHex()
            });
            Assert.Empty(_standaloneContext.AgentAddresses);
        }

        private string CreateSecretToken() => Guid.NewGuid().ToString();

        private void ConfigureSecretToken()
        {
            _configuration[GraphQLService.SecretTokenKey] = CreateSecretToken();
        }

        private void ConfigureAdminClaim()
        {
            _httpContextAccessor.HttpContext!.User.AddIdentity(new ClaimsIdentity(new[]
            {
                new Claim("role", "Admin"),
            }));
        }

        private void ConfigureNineChroniclesNodeService()
        {
            var consensusKey = new PrivateKey();
            
            _standaloneContext.NineChroniclesNodeService = new NineChroniclesNodeService(
                new PrivateKey(),
                new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
                {
                    GenesisBlock = _standaloneContext.BlockChain!.Genesis,
                    StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                    AppProtocolVersion = AppProtocolVersion.Sign(new PrivateKey(), 0),
                    SwarmPrivateKey = new PrivateKey(),
                    ConsensusPrivateKey = consensusKey,
                    ConsensusPort = 5000,
                    Validators = new List<PublicKey>()
                    {
                        consensusKey.PublicKey,
                    },
                    Host = IPAddress.Loopback.ToString(),
                },
                NineChroniclesNodeService.GetBlockPolicy(NetworkType.Test),
                NetworkType.Test);
        }
    }
}
