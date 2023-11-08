using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NineChronicles.Headless;

public class ArenaMemoryCache
{
    public MemoryCache Cache { get; } = new(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
    {
        SizeLimit = null
    }));
}
