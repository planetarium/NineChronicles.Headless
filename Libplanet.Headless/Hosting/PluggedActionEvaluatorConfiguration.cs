namespace Libplanet.Headless.Hosting;

public class PluggedActionEvaluatorConfiguration : IActionEvaluatorConfiguration
{
    public ActionEvaluatorType Type => ActionEvaluatorType.PluggedActionEvaluator;

    public string PluginPath { get; init; }

    public string TypeName => "Lib9c.Plugin.PluginActionEvaluator";
}
