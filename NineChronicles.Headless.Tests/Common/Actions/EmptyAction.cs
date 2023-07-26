using System;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;

namespace NineChronicles.Headless.Tests.Common.Actions
{
    public class EmptyAction : IAction
    {
        public void LoadPlainValue(IValue plainValue)
        {
        }

        public IAccountStateDelta Execute(IActionContext context)
        {
            return context.PreviousState;
        }

        public void Render(IActionContext context, IAccountStateDelta nextStates)
        {
        }

        public void RenderError(IActionContext context, Exception exception)
        {
        }

        public void Unrender(IActionContext context, IAccountStateDelta nextStates)
        {
        }

        public void UnrenderError(IActionContext context, Exception exception)
        {
        }

        public IValue PlainValue => new Null();
    }
}
