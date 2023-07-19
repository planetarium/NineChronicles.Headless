using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.State;

namespace Libplanet.Extensions.ForkableActionEvaluator;

public class BypassActionLoader : IActionLoader
{
    public IAction LoadAction(long index, IValue value)
    {
        return new NoOpAction
        {
            PlainValue = value,
        };
    }

    private class NoOpAction : IAction
    {
        public IValue PlainValue { get; set; }

        public void LoadPlainValue(IValue plainValue)
        {
            PlainValue = plainValue;
        }

        public IAccountStateDelta Execute(IActionContext context)
        {
            return context.PreviousState;
        }
    }
}
