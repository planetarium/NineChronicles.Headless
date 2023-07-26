using Bencodex.Types;
#if LIB9C_DEV_EXTENSIONS
using Lib9c.DevExtensions.Action.Loader;
#endif
using Libplanet.Action.Loader;
using Nekoyume.Action;
using Nekoyume.Action.Loader;

namespace NineChronicles.Headless;

public static class NCActionUtils
{
#if LIB9C_DEV_EXTENSIONS
    private static readonly IActionLoader _actionLoader = new NCDevActionLoader();
#else
    private static readonly IActionLoader _actionLoader = new NCActionLoader();
#endif

    // FIXME: Arbitrary 0 index is probably bad.
    public static ActionBase ToAction(IValue plainValue) => (ActionBase)_actionLoader.LoadAction(0, plainValue);
}
