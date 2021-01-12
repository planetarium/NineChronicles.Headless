using System;
using System.IO;
using System.Security.Claims;
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
            
            _controller = new GraphQLController(_standaloneContext, _httpContextAccessor, _configuration);
        }

        [Fact]
        public void RunStandaloneThrowsUnauthorizedIfSecretTokenUsed()
        {
            ConfigureSecretToken();
            Assert.IsType<UnauthorizedResult>(_controller.RunStandalone());
        }

        [Fact]
        public void RunStandaloneThrowsConflict()
        {
            _standaloneContext.NineChroniclesNodeService = null;
            IActionResult result = _controller.RunStandalone();
            Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, ((StatusCodeResult)result).StatusCode);
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RunStandalone(bool useSecretToken)
        {
            if (useSecretToken)
            {
                ConfigureSecretToken();
                ConfigureAdminClaim();
            }

            ConfigureNineChroniclesNodeService();
            Assert.IsType<OkObjectResult>(_controller.RunStandalone());
        }

        private string CreateSecretToken() => Guid.NewGuid().ToString();

        private void ConfigureSecretToken()
        {
            _configuration[GraphQLService.SecretTokenKey] = CreateSecretToken();
        }

        private void ConfigureAdminClaim()
        {
            _httpContextAccessor.HttpContext.User.AddIdentity(new ClaimsIdentity(new[]
            {
                new Claim("role", "Admin"), 
            }));
        }
    }
}
