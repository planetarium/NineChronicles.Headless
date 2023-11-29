using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace NineChronicles.Headless.Middleware;

public class JwtAuthenticationMiddleware : IMiddleware
{
    private readonly IConfiguration _configuration;

    public JwtAuthenticationMiddleware(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context);
    }
}
