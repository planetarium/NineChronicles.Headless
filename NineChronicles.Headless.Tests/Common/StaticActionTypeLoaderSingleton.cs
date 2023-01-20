using System.Reflection;
using Libplanet.Action;
using Nekoyume.Action;

namespace NineChronicles.Headless.Tests.Common;

public static class StaticActionTypeLoaderSingleton
{
    public static readonly StaticActionTypeLoader Instance = new StaticActionTypeLoader(
        Assembly.GetEntryAssembly() is { } entryAssembly
            ? new[] { typeof(ActionBase).Assembly, entryAssembly }
            : new[] { typeof(ActionBase).Assembly },
        typeof(ActionBase)
    );
}
