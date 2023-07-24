using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using Libplanet;
using Libplanet.Tx;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class CustomRateLimitMiddleware : RateLimitMiddleware<CustomIpRateLimitProcessor>
    {
        private static Dictionary<string, int> _stateQueryAgentList = new();
        private static Dictionary<string, DateTimeOffset> _blockedAgentList = new();
        private readonly ILogger _logger;
        private readonly IRateLimitConfiguration _config;
        private readonly IOptions<CustomIpRateLimitOptions> _options;

        public CustomRateLimitMiddleware(RequestDelegate next,
            IProcessingStrategy processingStrategy,
            IOptions<CustomIpRateLimitOptions> options,
            IIpPolicyStore policyStore,
            IRateLimitConfiguration config)
            : base(next, options?.Value, new CustomIpRateLimitProcessor(options?.Value!, policyStore, processingStrategy), config)
        {
            _config = config;
            _options = options!;
            _logger = Log.Logger.ForContext<CustomRateLimitMiddleware>();
        }

        protected override void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity, RateLimitCounter counter, RateLimitRule rule)
        {
            _logger.Information($"[IP-RATE-LIMITER] Request {identity.HttpVerb}:{identity.Path} from IP {identity.ClientIp} has been blocked, " +
                                $"quota {rule.Limit}/{rule.Period} exceeded by {counter.Count - rule.Limit}. Blocked by rule {rule.Endpoint}, " +
                                $"TraceIdentifier {httpContext.TraceIdentifier}. MonitorMode: {rule.MonitorMode}");
            if (counter.Count - rule.Limit >= _options.Value.IpBanThresholdCount)
            {
                _logger.Information($"[IP-RATE-LIMITER] Banning IP {identity.ClientIp}.");
                IpBanMiddleware.BanIp(identity.ClientIp);
            }
        }

        public override async Task<ClientRequestIdentity> ResolveIdentityAsync(HttpContext httpContext)
        {
            var identity = await base.ResolveIdentityAsync(httpContext);

            if (httpContext.Request.Protocol == "HTTP/1.1")
            {
                httpContext.Request.EnableBuffering();
                var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                httpContext.Request.Body.Seek(0, SeekOrigin.Begin);
                var agent = string.Empty;

                if (body.Contains("stageTransaction"))
                {
                    identity.Path = "/graphql/stagetransaction";
                    byte[] payload = ByteUtil.ParseHex(body.Split("\\\"")[1]);
                    Transaction tx = Transaction.Deserialize(payload);
                    if (_blockedAgentList.ContainsKey(tx.Signer.ToString()))
                    {
                        httpContext.Abort();
                    }
                }

                if (body.Contains("agent(address:"))
                {
                    agent = body.Split("\\\"")[1];
                    if (!_stateQueryAgentList.ContainsKey(agent))
                    {
                        _stateQueryAgentList.Add(agent, 1);
                    }
                    else
                    {
                        _stateQueryAgentList[agent] += 1;
                    }

                    _logger.Information("[IP-RATE-LIMITER] State Query signer: {signer} IP: {ip} Count: {count}.", agent, httpContext.Connection.RemoteIpAddress, _stateQueryAgentList[agent]);
                }

                if (agent != string.Empty && _stateQueryAgentList[agent] > 100)
                {
                    if (!_blockedAgentList.ContainsKey(agent))
                    {
                        _blockedAgentList.Add(agent, DateTimeOffset.Now);
                    }
                    else
                    {
                        if ((DateTimeOffset.Now - _blockedAgentList[agent]).Minutes >= 10)
                        {
                            _logger.Information("[IP-RATE-LIMITER] State Query signer: {signer} removed from blocked list.", agent);
                            _blockedAgentList.Remove(agent);
                            _stateQueryAgentList.Remove(agent);
                        }
                        else
                        {
                            _logger.Information("[IP-RATE-LIMITER] State Query signer: {signer} blocked for the next {time} minutes.", agent, 10 - (DateTimeOffset.Now - _blockedAgentList[agent]).Minutes);
                        }
                    }

                    httpContext.Abort();
                }

                return identity;
            }

            return identity;
        }
    }
}
