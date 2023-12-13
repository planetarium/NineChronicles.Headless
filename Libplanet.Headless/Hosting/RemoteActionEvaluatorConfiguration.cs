namespace Libplanet.Headless.Hosting;

public class RemoteActionEvaluatorConfiguration : IActionEvaluatorConfiguration
{
    public ActionEvaluatorType Type => ActionEvaluatorType.RemoteActionEvaluator;

    public string StateServiceEndpoint { get; init; }
}
