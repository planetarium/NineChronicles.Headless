using System.Collections.Immutable;
using System.Linq;

namespace Libplanet.Headless.Hosting;

public class ForkableActionEvaluatorConfiguration : IActionEvaluatorConfiguration
{
    public ActionEvaluatorType Type => ActionEvaluatorType.ForkableActionEvaluator;

    public ImmutableArray<(ForkableActionEvaluatorRange, IActionEvaluatorConfiguration)> Pairs { get; init; }
}

public class ForkableActionEvaluatorRange
{
    public long Start { get; set; }
    public long End { get; set; }
}
