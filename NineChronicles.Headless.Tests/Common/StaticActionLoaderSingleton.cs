using System.Reflection;
using Libplanet.Action;
using Nekoyume.Action;

namespace NineChronicles.Headless.Tests.Common;

public static class StaticActionLoaderSingleton
{
    public static readonly StaticActionLoader Instance = new StaticActionLoader(
        Assembly.GetEntryAssembly() is { } entryAssembly
            ? new[] { typeof(ActionBase).Assembly, entryAssembly }
            : new[] { typeof(ActionBase).Assembly },
        typeof(ActionBase)
    );
}
