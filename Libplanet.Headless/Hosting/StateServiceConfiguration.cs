namespace Libplanet.Headless.Hosting
{
    public class StateServiceConfiguration
    {
        public string Path { get; set; } = null!;

        public ushort Port { get; set; } = 11111;
        public StateServiceRange Range { get; set; } = null!;
        public string StateStorePath { get; set; } = null!;
    }
}
