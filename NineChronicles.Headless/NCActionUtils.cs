using Bencodex.Types;
using Libplanet.Action.Loader;
using Nekoyume.Action;
using Nekoyume.Action.Loader;

namespace NineChronicles.Headless;

public static class NCActionUtils
{
    private static readonly IActionLoader _actionLoader = new NCActionLoader();

    // FIXME: Arbitrary 0 index is probably bad.
    public static ActionBase ToAction(IValue plainValue) => (ActionBase)_actionLoader.LoadAction(0, plainValue);
}
