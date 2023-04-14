namespace NineChronicles.Headless.Properties
{
    public class GrpcRateLimitOptions
    {
        public const string GrpcRateLimit = "GrpcRateLimit";
        public int PermitLimit { get; set; } = 1;
        public int Window { get; set; } = 5;
        public int QueueLimit { get; set; } = 0;
        public bool AutoReplenishment { get; set; } = true;
    }
}
