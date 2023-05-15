using System.Runtime.Loader;
using Libplanet.Action;
using Libplanet.Blocks;

namespace Libplanet.Extensions.SandboxedActionEvaluator;

public class SandboxedActionEvaluator : IActionEvaluator
{
    public SandboxedActionEvaluator()
    {

    }

    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<IActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {

    }
}
