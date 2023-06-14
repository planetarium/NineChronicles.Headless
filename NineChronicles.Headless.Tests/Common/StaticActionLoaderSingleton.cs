using Libplanet.Action.Loader;
using Nekoyume.Action.Loader;

namespace NineChronicles.Headless.Tests.Common;

public static class StaticActionLoaderSingleton
{
    public static readonly IActionLoader Instance = new NCActionLoader();
}
