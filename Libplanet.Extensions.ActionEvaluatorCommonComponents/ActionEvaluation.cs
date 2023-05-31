using Bencodex.Types;
using Libplanet.Action;
using Libplanet.State;

namespace Libplanet.Extensions.ActionEvaluatorCommonComponents;

public class ActionEvaluation : IActionEvaluation
{
    public ActionEvaluation(
        IValue action,
        ActionContext inputContext,
        AccountStateDelta outputStates,
        Exception? exception,
        List<string> logs)
    {
        Action = action;
        InputContext = inputContext;
        OutputStates = outputStates;
        Exception = exception;
        Logs = logs;
    }

    public IValue Action { get; }
    public ActionContext InputContext { get; }
    IActionContext IActionEvaluation.InputContext => InputContext;
    public AccountStateDelta OutputStates { get; }
    IAccountStateDelta IActionEvaluation.OutputStates => OutputStates;
    public Exception? Exception { get; }
    public List<string> Logs { get; }
}
