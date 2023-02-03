using Microsoft.Extensions.Configuration;

namespace NineChronicles.Headless.Properties
{
    public class GraphQLNodeServiceProperties
    {
        public bool GraphQLServer { get; set; }

        public string? GraphQLListenHost { get; set; }

        public int? GraphQLListenPort { get; set; }

        public string? SecretToken { get; set; }

        public bool NoCors { get; set; }

        public bool UseMagicOnion { get; set; }

        public MagicOnionHttpOptions? HttpOptions { get; set; }

        public readonly struct MagicOnionHttpOptions
        {
            public MagicOnionHttpOptions(string target)
            {
                Target = target;
            }

            public string Target { get; }
        }
    }
}
