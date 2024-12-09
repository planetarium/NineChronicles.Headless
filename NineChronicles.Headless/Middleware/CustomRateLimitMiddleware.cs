using System.IO;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;
using Serilog;
using ILogger = Serilog.ILogger;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace NineChronicles.Headless.Middleware
{

    public class CustomRateLimitMiddleware : RateLimitMiddleware<CustomIpRateLimitProcessor>
    {
        private readonly ILogger _logger;
        private readonly IRateLimitConfiguration _config;
        private readonly IOptions<CustomIpRateLimitOptions> _options;
        private readonly string _whitelistedIp;
        private readonly System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler _tokenHandler = new();
        private readonly Microsoft.IdentityModel.Tokens.TokenValidationParameters _validationParams;

        public CustomRateLimitMiddleware(RequestDelegate next,
            IProcessingStrategy processingStrategy,
            IOptions<CustomIpRateLimitOptions> options,
            IIpPolicyStore policyStore,
            IRateLimitConfiguration config,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
            : base(next, options?.Value, new CustomIpRateLimitProcessor(options?.Value!, policyStore, processingStrategy), config)
        {
            _config = config;
            _options = options!;
            _logger = Log.Logger.ForContext<CustomRateLimitMiddleware>();
            var jwtConfig = configuration.GetSection("Jwt");
            var issuer = jwtConfig["Issuer"] ?? "";
            var key = jwtConfig["Key"] ?? "";
            _whitelistedIp = configuration.GetSection("IpRateLimiting:IpWhitelist")?.Get<string[]>()?.FirstOrDefault() ?? "127.0.0.1";
            _validationParams = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(key.PadRight(512 / 8, '\0')))
            };
        }

        protected override void LogBlockedRequest(HttpContext context, ClientRequestIdentity identity, RateLimitCounter counter, RateLimitRule rule)
        {
            _logger.Information($"[IP-RATE-LIMITER] Request {identity.HttpVerb}:{identity.Path} from IP {identity.ClientIp} has been blocked, " +
                                $"quota {rule.Limit}/{rule.Period} exceeded by {counter.Count - rule.Limit}. Blocked by rule {rule.Endpoint}, " +
                                $"TraceIdentifier {context.TraceIdentifier}. MonitorMode: {rule.MonitorMode}");
            if (counter.Count - rule.Limit >= _options.Value.IpBanThresholdCount)
            {
                _logger.Information($"[IP-RATE-LIMITER] Banning IP {identity.ClientIp}.");
                IpBanMiddleware.BanIp(identity.ClientIp);
            }
        }

        public override async Task<ClientRequestIdentity> ResolveIdentityAsync(HttpContext context)
        {
            var identity = await base.ResolveIdentityAsync(context);

            if (context.Request.Protocol == "HTTP/1.1")
            {
                var body = context.Items["RequestBody"]!.ToString()!;
                context.Request.Body.Seek(0, SeekOrigin.Begin);
                if (body.Contains("stageTransaction"))
                {
                    identity.Path = "/graphql/stagetransaction";
                }
            }

            // Check for JWT secret key in headers
            if (context.Request.Headers.TryGetValue("Authorization", out var authHeaderValue) &&
                authHeaderValue.Count > 0)
            {
                try
                {
                    var (scheme, token) = ExtractSchemeAndToken(authHeaderValue);
                    if (scheme.Equals("Bearer", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _tokenHandler.ValidateToken(token, _validationParams, out _);
                        _logger.Information("[IP-RATE-LIMITER] Valid JWT token provided. Updating ClientIp to whitelisted IP.");
                        identity.ClientIp = _whitelistedIp;
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.Warning("[IP-RATE-LIMITER] JWT validation failed: {Message}", ex.Message);
                }
            }

            return identity;
        }

        private (string scheme, string token) ExtractSchemeAndToken(Microsoft.Extensions.Primitives.StringValues authorizationHeader)
        {
            if (authorizationHeader.Count == 0 || string.IsNullOrWhiteSpace(authorizationHeader[0]))
            {
                throw new System.ArgumentException("Authorization header is missing or empty.");
            }

            var headerValues = authorizationHeader[0]!.Split(" ");
            if (headerValues.Length != 2)
            {
                throw new System.ArgumentException("Invalid Authorization header format. Expected 'Scheme Token'.");
            }

            return (headerValues[0], headerValues[1]);
        }
    }
}
