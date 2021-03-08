using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using GraphQL;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NineChronicles.Headless.GraphTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class AuthenticationMutationTest : IDisposable
    {
        private readonly Web3KeyStore _keyStore;
        private readonly PrivateKey _privateKey;
        private readonly string _passphrase;
        private readonly ServiceCollection _services;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationMutationTest()
        {
            _keyStore = GraphQLTestUtils.CreateRandomWeb3KeyStore();
            _httpContextAccessor = new HttpContextAccessor();
            _httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                Session = new InMemorySession(string.Empty, true),
            };
            
            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(_ => _.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.FromResult((object)null));
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(_ => _.GetService(typeof(IAuthenticationService)))
                .Returns(authServiceMock.Object);
            _httpContextAccessor.HttpContext.RequestServices = serviceProviderMock.Object;

            _privateKey = new PrivateKey();
            _passphrase = Guid.NewGuid().ToString();
            _keyStore.Add(ProtectedPrivateKey.Protect(_privateKey, _passphrase));
            _services = new ServiceCollection();
            _services.AddSingleton<IKeyStore>(_keyStore);
            _services.AddSingleton(_httpContextAccessor);
        }

        [Fact]
        public async Task Login_Success()
        {
            var result = await ExecuteAsync(LoginQuery(_privateKey.ToAddress(), _passphrase));
            Assert.Equal(true, result.Data.As<Dictionary<string, object>>()["login"]);
            Assert.Null(result.Errors);
        }

        [Fact]
        public async Task Login_ShouldFailWithNotExistedKey()
        {
            var result = await ExecuteAsync(LoginQuery(new PrivateKey().ToAddress(), _passphrase));
            Assert.Null(result.Data);
            Assert.NotNull(result.Errors);
        }

        [Fact]
        public async Task Login_ShouldFailWithIncorrectPassphrase()
        {
            var result = await ExecuteAsync(LoginQuery(_privateKey.ToAddress(), Guid.NewGuid().ToString()));
            Assert.Null(result.Data);
            Assert.NotNull(result.Errors);
        }

        [Fact]
        public async Task Logout()
        {
            var result = await ExecuteAsync(LogoutQuery());
            Assert.Equal(false, result.Data.As<Dictionary<string, object>>()["logout"]);
            Assert.NotNull(result.Errors);
        }

        private string LoginQuery(Address address, string passphrase) =>
            @$"mutation {{ login(address: ""{address}"", passphrase: ""{passphrase}"") }}";

        private string LogoutQuery() =>
            "mutation { logout }";

        private Task<ExecutionResult> ExecuteAsync(string query)
        {
            return GraphQLTestUtils.ExecuteQueryAsync<AuthenticationMutation>(_services, query, executionMode: GraphQLTestUtils.ExecutionMode.Mutation, source: new object());
        }
        

        public void Dispose()
        {
            Directory.Delete(_keyStore.Path, true);
        }
    }
}
