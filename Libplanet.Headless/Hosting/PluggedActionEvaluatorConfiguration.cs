namespace Libplanet.Headless.Hosting;

public class PluggedActionEvaluatorConfiguration : IActionEvaluatorConfiguration
{
    /// <summary>
    /// Default version of the <see cref="PluggedActionEvaluatorConfiguration"/>'s schema.
    /// It must not be changed for backward-compatibility.
    /// </summary>
    public const long DefaultVersion = 1;

    public ActionEvaluatorType Type => ActionEvaluatorType.PluggedActionEvaluator;

    /// <summary>
    /// Gets the version of the <see cref="PluggedActionEvaluatorConfiguration"/>'s schema.
    /// </summary>
    /// <remarks>
    /// For backward compatibility, it uses default version as 1.
    /// </remarks>
    public long Version { get; init; } = DefaultVersion;

    public string PluginPath { get; init; }

    public string TypeName => "Lib9c.Plugin.PluginActionEvaluator";
}
