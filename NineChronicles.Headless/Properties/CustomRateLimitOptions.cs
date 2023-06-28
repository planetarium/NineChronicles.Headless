using AspNetCoreRateLimit;

namespace NineChronicles.Headless.Properties
{
    public class CustomIpRateLimitOptions : IpRateLimitOptions
    {
        public int IpBanThresholdCount { get; set; } = 10;

        public int IpBanMinute { get; set; } = 60;

        public IpBanResponse? IpBanResponse { get; set; }
    }
}
