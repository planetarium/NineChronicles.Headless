using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Serilog;

namespace NineChronicles.Headless.Middleware;

public class JwtAuthenticationMiddleware : IMiddleware
{
    private readonly ILogger _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
    private readonly TokenValidationParameters _validationParams;

    public JwtAuthenticationMiddleware(IConfiguration configuration)
    {
        _logger = Log.Logger.ForContext<JwtAuthenticationMiddleware>();
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key.PadRight(512 / 8, '\0')))
        };
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Headers.TryGetValue("Authorization", out var authorization);
        if (authorization.Count > 0)
        {
            try
            {
                var (scheme, token) = ExtractSchemeAndToken(authorization);
                if (scheme == "Bearer")
                {
                    ValidateTokenAndAddClaims(context, token);
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Authorization error {e.Message}");
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    JsonConvert.SerializeObject(
                        new { errpr = e.Message }
                        ));
                return;
            }
        }
        await next(context);
    }

    private (string scheme, string token) ExtractSchemeAndToken(StringValues authorizationHeader)
    {
        if (authorizationHeader[0]?.Split(" ") is not string[] headerValues)
        {
            throw new ArgumentException("Authorization header isn't given.");
        }

        if (headerValues.Length < 2)
        {
            throw new ArgumentException("Invalid Authorization header format.");
        }

        return (headerValues[0], headerValues[1]);
    }

    private void ValidateTokenAndAddClaims(HttpContext context, string token)
    {
        _tokenHandler.ValidateToken(token, _validationParams, out SecurityToken validatedToken);
        var jwt = (JwtSecurityToken)validatedToken;
        var claims = jwt.Claims.Select(claim => new Claim(claim.Type, claim.Value));
        context.User.AddIdentity(new ClaimsIdentity(claims));
    }
}
