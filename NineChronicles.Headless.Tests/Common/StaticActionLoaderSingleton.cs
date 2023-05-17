using Libplanet.Action;
using Libplanet.Action.Loader;
using Nekoyume.Action;

namespace NineChronicles.Headless.Tests.Common;

public static class StaticActionLoaderSingleton
{
    public static readonly IActionLoader Instance = new SingleActionLoader(typeof(PolymorphicAction<ActionBase>));
}
