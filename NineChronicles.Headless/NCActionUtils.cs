using Bencodex.Types;

namespace NineChronicles.Headless;

using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

public static class NCActionUtils
{
    public static NCAction ToAction(IValue plainValue)
    {
#pragma warning disable CS0612
        var action = new NCAction();
#pragma warning restore CS0612
        action.LoadPlainValue(plainValue);
        return action;
    }
}
