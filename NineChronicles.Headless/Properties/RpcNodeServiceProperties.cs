namespace NineChronicles.Headless.Properties
{
    public struct RpcNodeServiceProperties
    {
        public string RpcListenHost { get; set; }

        public int RpcListenPort { get; set; }

        public bool RpcRemoteServer { get; set; }

        public MagicOnionHttpOptions? HttpOptions { get; set; }

        public readonly struct MagicOnionHttpOptions
        {
            public MagicOnionHttpOptions(string host, int port)
            {
                Host = host;
                Port = port;
            }

            public string Host { get; }

            public int Port { get; }
        }
    }
}
