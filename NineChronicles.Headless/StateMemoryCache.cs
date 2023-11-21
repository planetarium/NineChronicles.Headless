using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NineChronicles.Headless;

public class StateMemoryCache
{
    public MemoryCache ArenaParticipantsCache { get; } = new(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
    {
        SizeLimit = null
    }));

    public MemoryCache SheetCache { get; } = new(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
    {
        SizeLimit = null
    }));
}
