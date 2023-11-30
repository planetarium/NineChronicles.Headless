using System.Reflection;
using System.Security.Cryptography;
using Lib9c.Plugin.Shared;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Common;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Types.Blocks;

namespace Libplanet.Extensions.PluggedActionEvaluator
{
    public class PluggedActionEvaluator : IActionEvaluator
    {
        private readonly IPluginActionEvaluator _pluginActionEvaluator;

        public IActionLoader ActionLoader => throw new NotImplementedException();

        public PluggedActionEvaluator(string pluginPath, string typeName, string stateStorePath)
        {
            _pluginActionEvaluator = CreateActionEvaluator(pluginPath, typeName, stateStorePath);
        }

        public static Assembly LoadPlugin(string absolutePath)
        {
            PluginLoadContext loadContext = new PluginLoadContext(absolutePath);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(absolutePath)));
        }

        public static IPluginActionEvaluator CreateActionEvaluator(Assembly assembly, string typeName, string stateStorePath)
        {
            if (assembly.GetType(typeName) is Type type &&
                Activator.CreateInstance(type, args: stateStorePath) as IPluginActionEvaluator
                is IPluginActionEvaluator pluginActionEvaluator)
            {
                return pluginActionEvaluator;
            }

            throw new NullReferenceException("PluginActionEvaluator not found with given parameters");
        }

        public static IPluginActionEvaluator CreateActionEvaluator(string pluginPath, string typeName, string stateStorePath)
            => CreateActionEvaluator(LoadPlugin(pluginPath), typeName, stateStorePath);

        public IReadOnlyList<ICommittedActionEvaluation> Evaluate(IPreEvaluationBlock block, HashDigest<SHA256>? baseStateRootHash)
            => _pluginActionEvaluator.Evaluate(
                PreEvaluationBlockMarshaller.Serialize(block),
                baseStateRootHash is { } srh ? srh.ToByteArray() : null)
                .Select(eval => ActionEvaluationMarshaller.Deserialize(eval)).ToList().AsReadOnly();
    }
}
