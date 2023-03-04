using Libplanet.Headless;

namespace NineChronicles.Headless.Executable;

public record ActionTypeLoaderConfiguration
{
    public DynamicActionTypeLoaderConfiguration? DynamicActionTypeLoader { get; init; }
    public StaticActionTypeLoaderConfiguration? StaticActionTypeLoader { get; init; }
}
