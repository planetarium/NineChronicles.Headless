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
        private readonly int _banCount;
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
            _banCount = configuration.GetValue<int>("IpRateLimiting:TransactionResultsBanThresholdCount", 100);
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
                else if (body.Contains("transactionResults"))
                {
                    identity.Path = "/graphql/transactionresults";

                    // Check for txIds count
                    var txIdsCount = CountTxIds(body);
                    if (txIdsCount > _banCount)
                    {
                        _logger.Information($"[IP-RATE-LIMITER] Banning IP {identity.ClientIp} due to excessive txIds count: {txIdsCount}");
                        IpBanMiddleware.BanIp(identity.ClientIp);
                    }
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

        private int CountTxIds(string body)
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(body);

                // Check for txIds in query variables first
                if (json.RootElement.TryGetProperty("variables", out var variables) &&
                    variables.TryGetProperty("txIds", out var txIdsElement) &&
                    txIdsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    // Count from variables
                    return txIdsElement.GetArrayLength();
                }

                // Fallback to check the query string if variables are not set
                if (json.RootElement.TryGetProperty("query", out var queryElement))
                {
                    var query = queryElement.GetString();
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        // Extract txIds from the query string using regex
                        var txIdMatches = System.Text.RegularExpressions.Regex.Matches(
                            query, @"transactionResults\s*\(\s*txIds\s*:\s*\[(?<txIds>[^\]]*)\]"
                        );

                        if (txIdMatches.Count > 0)
                        {
                            // Extract the inner contents of txIds
                            var txIdList = txIdMatches[0].Groups["txIds"].Value;

                            // Count txIds using commas
                            var txIds = txIdList.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

                            return txIds.Length;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.Warning("[IP-RATE-LIMITER] Error parsing request body: {Message}", ex.Message);
            }

            // Return 0 if txIds not found
            return 0;
        }
    }
}
