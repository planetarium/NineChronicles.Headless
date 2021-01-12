using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace NineChronicles.Headless.Middleware
{
    public class LocalAuthenticationMiddleware : IMiddleware
    {
        private readonly IConfiguration _configuration;

        public LocalAuthenticationMiddleware(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (_configuration[GraphQLService.SecretTokenKey] is { } secretToken
                && context.Request.Headers.TryGetValue("Authorization", out StringValues v)
                && v.Count == 1 && v[0] == $"Basic {secretToken}")
            {
                context.User.AddIdentity(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(
                                "role",
                                "Admin"),
                        }));
            }

            await next(context);
        }
    }
}
