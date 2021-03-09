using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NineChronicles.Headless.Middleware;
using Xunit;

namespace NineChronicles.Headless.Tests.Middleware
{
    public class AuthenticationValidationMiddlewareTest
    {
        private readonly IMiddleware _middleware;
        private readonly HttpContext _httpContext;
        private readonly RequestDelegate _requestDelegate;

        public AuthenticationValidationMiddlewareTest()
        {
            var services = new ServiceCollection();
            _httpContext = new DefaultHttpContext();
            services.AddSingleton(_httpContext);
            services.AddLogging().AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
            _httpContext.RequestServices = services.BuildServiceProvider();
            _httpContext.Session = new InMemorySession(string.Empty, true);
            _requestDelegate = hc => Task.CompletedTask;
            _middleware = new AuthenticationValidationMiddleware();
        }

        [Fact]
        public async Task InvokeAsync_SignOut_IfPrivateKeyDoesNotExist()
        {
            var userRoleClaim = new Claim(ClaimTypes.Role, "User");
            bool IsSignedIn() => _httpContext.User.HasClaim(userRoleClaim.Type, userRoleClaim.Value);
            var identity = new ClaimsIdentity(new List<Claim> {userRoleClaim}, "Login");
            await _httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new List<ClaimsIdentity>
            {
                identity
            }));
            _httpContext.User.AddIdentity(identity);
            Assert.True(IsSignedIn());
            Assert.Null(_httpContext.Session.GetPrivateKey());

            await _middleware.InvokeAsync(
                _httpContext,
                _requestDelegate);

            Assert.True(_httpContext.Response.Headers.Contains(new KeyValuePair<string, StringValues>(
                "Set-Cookie",
                ".AspNetCore.Cookies=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; samesite=lax; httponly")));
        }
    }
}
