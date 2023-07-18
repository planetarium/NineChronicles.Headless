using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blocks;

namespace Libplanet.Extensions.ForkableActionEvaluator;

public class ForkableActionEvaluator : IActionEvaluator
{
    private readonly HardForkRouter _router;

    public ForkableActionEvaluator(IEnumerable<((long StartIndex, long EndIndex) Range, IActionEvaluator ActionEvaluator)> pairs, IActionLoader actionLoader)
    {
        _router = new HardForkRouter(pairs);
        ActionLoader = actionLoader;
    }

    public IActionLoader ActionLoader { get; }

    public IReadOnlyList<IActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {
        var actionEvaluator = _router.GetEvaluator(block.Index);
        return actionEvaluator.Evaluate(block);
    }
}
