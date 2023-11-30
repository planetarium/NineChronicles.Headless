using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace NineChronicles.Headless.Middleware;

public class JwtAuthenticationMiddleware : IMiddleware
{
    
    private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
    private readonly TokenValidationParameters _validationParams;

    public JwtAuthenticationMiddleware(IConfiguration configuration)
    {
        var jwtConfig = configuration.GetSection("Jwt");
        var issuer = jwtConfig["Issuer"] ?? "";
        var key = jwtConfig["Key"] ?? "";
        _validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key.PadRight(512/8, '\0')))
        };
    }
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Headers.TryGetValue("Authorization", out var authorization);
        if (authorization.Count > 0)
        {
            try
            {
                string[] headerValues = authorization[0].Split(" ");
                string scheme = headerValues[0];
                if (scheme == "Bearer")
                {
                    string token = headerValues[1];

                    _tokenHandler.ValidateToken(token, _validationParams, out SecurityToken validatedToken);
                    var jwt = (JwtSecurityToken)validatedToken;
                    foreach (var claim in jwt.Claims)
                    {
                        context.User.AddIdentity(
                            new ClaimsIdentity(
                                new[]
                                {
                                    new Claim(claim.Type, claim.Value)
                                }));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Authorization error {e.Message}");
            }
        }
        await next(context);
    }
}
