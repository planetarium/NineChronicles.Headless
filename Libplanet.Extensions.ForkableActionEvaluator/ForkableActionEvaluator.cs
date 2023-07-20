using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Types.Blocks;

namespace Libplanet.Extensions.ForkableActionEvaluator;

public class ForkableActionEvaluator : IActionEvaluator
{
    private readonly HardForkRouter _router;

    public ForkableActionEvaluator(IEnumerable<((long StartIndex, long EndIndex) Range, IActionEvaluator ActionEvaluator)> pairs)
    {
        _router = new HardForkRouter(pairs);
    }

    public IActionLoader ActionLoader => throw new NotSupportedException();

    public IReadOnlyList<IActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {
        var actionEvaluator = _router.GetEvaluator(block.Index);
        return actionEvaluator.Evaluate(block);
    }
}
