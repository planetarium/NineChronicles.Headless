using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Libplanet.Crypto;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NineChronicles.Headless.GraphTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class MinerMutationTest
    {
        private readonly StandaloneContext _standaloneContext;
        private IHttpContextAccessor _httpContextAccessor;
        private readonly PrivateKey _privateKey;
        private ServiceCollection _services;
        private readonly IMiner _miner;

        private bool IsMining { get; set; }

        public MinerMutationTest()
        {
            _httpContextAccessor = new HttpContextAccessor();
            _httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                Session = new InMemorySession(string.Empty, true),
            };

            Mock<IMiner> minerMock = new Mock<IMiner>();
            minerMock.Setup(_ => _.StartMining()).Callback(() => IsMining = true);
            minerMock.Setup(_ => _.StopMining()).Callback(() => IsMining = false);
            minerMock.SetupProperty(_ => _.PrivateKey);

            _miner = minerMock.Object;
            _privateKey = new PrivateKey();

            _standaloneContext = new StandaloneContext();
            _services = new ServiceCollection();
            _services.AddSingleton(_httpContextAccessor);
            _services.AddSingleton(_miner);
        }

        [Fact]
        public async Task Start()
        {
            Assert.Null(_miner.PrivateKey);

            _httpContextAccessor.HttpContext.Session.SetPrivateKey(_privateKey);
            var result = await ExecuteAsync("mutation { start }");
            Assert.Null(result.Errors);
            Assert.Equal(_privateKey, _miner.PrivateKey);
            Assert.Equal(true, result.Data.As<IDictionary<string, object>>()["start"]);
            Assert.True(IsMining);
        }
        
        [Fact]
        public async Task Start_WithoutSessionPrivateKey_ShouldFail()
        {
            var result = await ExecuteAsync("mutation { start }");
            Assert.NotNull(result.Errors);
            Assert.Equal(false, result.Data.As<IDictionary<string, object>>()["start"]);
            Assert.False(IsMining);
        }

        [Fact]
        public async Task Stop()
        {
            var result = await ExecuteAsync("mutation { stop }");

            Assert.Null(result.Errors);
            Assert.Equal(true, result.Data.As<IDictionary<string, object>>()["stop"]);
            Assert.False(IsMining);
        }

        private Task<ExecutionResult> ExecuteAsync(string query)
        {
            return GraphQLTestUtils.ExecuteQueryAsync<MinerMutation>(
                _services,
                query,
                executionMode: GraphQLTestUtils.ExecutionMode.Mutation,
                source: new object(),
                standaloneContext: _standaloneContext);
        }
    }
}
