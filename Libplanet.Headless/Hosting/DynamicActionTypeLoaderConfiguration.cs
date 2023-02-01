namespace Libplanet.Headless;

public class DynamicActionTypeLoaderConfiguration
{
    public class HardFork
    {
        public long SinceBlockIndex { get; init; }

        public string VersionName { get; init; }
    }

    public string BasePath { get; init; }

    public string AssemblyFileName { get; init; }

    public HardFork[] HardForks { get; init; }
}
