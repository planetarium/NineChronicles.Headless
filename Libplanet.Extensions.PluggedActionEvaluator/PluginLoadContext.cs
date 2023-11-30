using System.Reflection;
using System.Runtime.Loader;

namespace Libplanet.Extensions.PluggedActionEvaluator
{
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (_resolver.ResolveAssemblyToPath(assemblyName) is { } assemblyPath)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            if (_resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } libraryPath)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}

