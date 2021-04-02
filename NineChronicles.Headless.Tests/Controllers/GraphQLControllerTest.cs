using System.IO;
using Libplanet.Action;
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
using Microsoft.Extensions.DependencyInjection;
using Nekoyume.Action;
using NineChronicles.Headless.Controllers;
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
            var stateStore = new TrieStateStore(
                new DefaultKeyValueStore(null),
                new DefaultKeyValueStore(null));
            var genesisBlock = BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock();
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
            _httpContextAccessor.HttpContext.Session = new InMemorySession(string.Empty, true);
            var services = new ServiceCollection();
            services.AddAuthentication();
            _httpContextAccessor.HttpContext.RequestServices = services.BuildServiceProvider();

            _controller = new GraphQLController(_standaloneContext, _httpContextAccessor, _configuration);
        }

        [Fact]
        public void RunStandaloneThrowsConflict()
        {
            _standaloneContext.NineChroniclesNodeService = null;
            IActionResult result = _controller.RunStandalone();
            Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, ((StatusCodeResult)result).StatusCode);
        }
        
        [Fact]
        public void RunStandalone()
        {
            ConfigureNineChroniclesNodeService();
            Assert.IsType<OkObjectResult>(_controller.RunStandalone());
        }

        private void ConfigureNineChroniclesNodeService()
        {
            _standaloneContext.NineChroniclesNodeService = new NineChroniclesNodeService(
                new PrivateKey(),
                new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
                {
                    MinimumDifficulty = 500000,
                    GenesisBlock = _standaloneContext.BlockChain!.Genesis,
                    StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                    AppProtocolVersion = AppProtocolVersion.Sign(new PrivateKey(), 0),
                    SwarmPrivateKey = new PrivateKey(),
                    Host = IPAddress.Loopback.ToString(),
                },
                null);
        }
    }
}
