using System.Linq;
using AspNetCoreRateLimit;
using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless.Middleware
{
    public class CustomIpRateLimitProcessor : IpRateLimitProcessor
    {
        private readonly CustomIpRateLimitOptions _options;

        public CustomIpRateLimitProcessor(
            CustomIpRateLimitOptions options,
            IIpPolicyStore policyStore,
            IProcessingStrategy processingStrategy) : base(options, policyStore, processingStrategy)
        {
            _options = options;
        }

        public override bool IsWhitelisted(ClientRequestIdentity requestIdentity)
        {
            if (_options.ClientWhitelist != null && _options.ClientWhitelist.Contains(requestIdentity.ClientId))
            {
                return true;
            }

            if (_options.IpWhitelist != null && IpParser.ContainsIp(
                    _options.IpWhitelist,
                    requestIdentity.ClientIp))
            {
                return true;
            }

            if (_options.EndpointWhitelist != null && _options.EndpointWhitelist.Any())
            {
                string path = _options.EnableRegexRuleMatching
                    ? $".+:{requestIdentity.Path}"
                    : $"*:{requestIdentity.Path}";

                if (_options.EndpointWhitelist.Any(x =>
                        $"{requestIdentity.HttpVerb}:{requestIdentity.Path}".IsUrlMatch(x,
                            _options.EnableRegexRuleMatching)) ||
                    _options.EndpointWhitelist.Any(x => path.IsUrlMatch(x, _options.EnableRegexRuleMatching)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
