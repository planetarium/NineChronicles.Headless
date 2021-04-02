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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Options;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class AuthenticationMutationTest
    {
        private readonly PrivateKey _privateKey;
        private readonly ServiceCollection _services;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly string _adminPassphrase;

        private ClaimsPrincipal? LatestSignedInPrincipal { get; set; } 

        public AuthenticationMutationTest()
        {
            _httpContextAccessor = new HttpContextAccessor();
            _httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                Session = new InMemorySession(string.Empty, true),
            };

            _adminPassphrase = Guid.NewGuid().ToString();
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>(nameof(AuthenticationMutationOptions.AdminPassphrase), _adminPassphrase)
            }).Build();

            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(_ => _.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<AuthenticationProperties>()))
                .Callback<HttpContext, string, ClaimsPrincipal, AuthenticationProperties>(
                    (httpContext, scheme, principal, _) =>
                    {
                        LatestSignedInPrincipal = principal;
                    })
                .Returns(Task.FromResult((object?) null));
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(_ => _.GetService(typeof(IAuthenticationService)))
                .Returns(authServiceMock.Object);
            _httpContextAccessor.HttpContext.RequestServices = serviceProviderMock.Object;

            _privateKey = new PrivateKey();
            _services = new ServiceCollection();
            _services.AddSingleton(_httpContextAccessor);
            _services.AddSingleton(_configuration);
        }

        [Fact]
        public async Task Login_Success()
        {
            var result = await ExecuteAsync(LoginQuery(_privateKey));
            Assert.Equal(true, result.Data.As<Dictionary<string, object>>()["login"]);
            Assert.Null(result.Errors);
        }
        
        [Fact]
        public async Task Login_WithAdminPassphrase_Success()
        {
            var result = await ExecuteAsync(LoginQuery(_privateKey, _adminPassphrase));
            
            Assert.Equal(true, result.Data.As<Dictionary<string, object>>()["login"]);
            Assert.Null(result.Errors);
            Assert.True(IsSignedInAsAdmin());
            Assert.False(IsSignedInAsUser());
        }
        
        [Fact]
        public async Task Login_WithIncorrectAdminPassphrase_ShouldFail()
        {
            var result = await ExecuteAsync(LoginQuery(_privateKey, ""));
            Assert.Null(result.Data);
            Assert.NotNull(result.Errors);
            Assert.False(IsSignedInAsAdmin());
            Assert.False(IsSignedInAsUser());
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("00")]
        public async Task Login_ShouldFailWithIncorrectPrivateKey(string incorrectPrivateKeyHex)
        {
            var result = await ExecuteAsync(LoginQuery(incorrectPrivateKeyHex));
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

        private string LoginQuery(PrivateKey privateKey, string? adminPassphrase = null) =>
            LoginQuery(ByteUtil.Hex(privateKey.ByteArray), adminPassphrase);

        private string LoginQuery(string privateKeyHex, string? adminPassphrase = null)
        {
            string arguments = @$"privateKey: ""{privateKeyHex}""";
            if (!(adminPassphrase is null))
            {
                arguments += @$", {nameof(adminPassphrase)}: ""{adminPassphrase}""";
            }

            return $"mutation {{ login({arguments}) }}";
        }

        private string LogoutQuery() =>
            "mutation { logout }";

        private bool IsSignedInAsAdmin() => LatestSignedInPrincipal?.HasClaim(ClaimTypes.Role, "Admin") ?? false;

        private bool IsSignedInAsUser() => LatestSignedInPrincipal?.HasClaim(ClaimTypes.Role, "User") ?? false;

        private Task<ExecutionResult> ExecuteAsync(string query)
        {
            return GraphQLTestUtils.ExecuteQueryAsync<AuthenticationMutation>(_services, query, executionMode: GraphQLTestUtils.ExecutionMode.Mutation, source: new object());
        }
    }
}
