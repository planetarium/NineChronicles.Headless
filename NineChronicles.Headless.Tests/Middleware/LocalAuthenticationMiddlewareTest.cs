using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NineChronicles.Headless.Middleware;
using Xunit;

namespace NineChronicles.Headless.Tests.Middleware
{
    public class LocalAuthenticationMiddlewareTest
    {
        private readonly IConfiguration _configuration;
        private readonly string _secretToken;
        private readonly IMiddleware _middleware;
        private readonly HttpContext _httpContext;
        private readonly RequestDelegate _requestDelegate;

        public LocalAuthenticationMiddlewareTest()
        {
            _secretToken = Guid.NewGuid().ToString();
            _httpContext = new DefaultHttpContext();
            _requestDelegate = hc => Task.CompletedTask;

            _configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            _middleware = new LocalAuthenticationMiddleware(_configuration);
        }

        [Fact]
        public async Task Authorize()
        {
            Assert.False(_httpContext.User.HasClaim("role", "Admin"));

            _configuration[GraphQLService.SecretTokenKey] = _secretToken;
            _httpContext.Request.Headers.Add("Authorization", $"Basic {_secretToken}");

            await _middleware.InvokeAsync(
                _httpContext,
                _requestDelegate);

            Assert.True(_httpContext.User.HasClaim("role", "Admin"));
        }

        [Theory]
        [MemberData(nameof(IncorrectHeaders))]
        public async Task AuthorizeWithIncorrectSecretToken(string incorrectHeader)
        {
            Assert.False(_httpContext.User.HasClaim("role", "Admin"));

            _configuration[GraphQLService.SecretTokenKey] = _secretToken;
            _httpContext.Request.Headers.Add("Authorization", incorrectHeader);

            await _middleware.InvokeAsync(
                _httpContext,
                _requestDelegate);

            Assert.False(_httpContext.User.HasClaim("role", "Admin"));
        }

        public static IEnumerable<object[]> IncorrectHeaders => new[]
        {
            new [] { "" },
            new [] { "Basic" },
            new [] { "Basic a" },
            new [] { $"Basic {Guid.NewGuid().ToString()}-" },
        };
    }
}
