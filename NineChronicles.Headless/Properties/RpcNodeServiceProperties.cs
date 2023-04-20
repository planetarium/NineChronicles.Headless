namespace NineChronicles.Headless.Properties
{
    public struct RpcNodeServiceProperties
    {
        public string RpcListenHost { get; set; }

        public int RpcListenPort { get; set; }

        public bool RpcRemoteServer { get; set; }

        public bool RpcRateLimiter { get; set; }

        public int RpcRateLimiterWindow { get; set; }

        public int RpcRateLimiterPermit { get; set; }
    }
}
