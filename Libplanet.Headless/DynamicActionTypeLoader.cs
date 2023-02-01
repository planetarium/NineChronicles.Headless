using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Libplanet.Action;
using Libplanet.Blocks;
using Serilog;

using HardFork = Libplanet.Headless.DynamicActionTypeLoaderConfiguration.HardFork;

namespace Libplanet.Headless;

public class DynamicActionTypeLoader : IActionTypeLoader
{
    private readonly string _basePath;
    private readonly string _assemblyFileName;
    private readonly IImmutableList<HardFork> _hardForks;
    private readonly IDictionary<string, Assembly> _cache;

    public DynamicActionTypeLoader(string basePath, string assemblyFileName, IOrderedEnumerable<HardFork> hardForks)
    {
        _basePath = basePath;
        _assemblyFileName = assemblyFileName;
        _hardForks = hardForks.ToImmutableList();
        _cache = new Dictionary<string, Assembly>();
    }

    public IDictionary<string, Type> Load(IActionTypeLoaderContext context)
    {
        var types = new Dictionary<string, Type>();

        foreach (Type type in LoadAllActionTypes(context))
        {
            if (ActionTypeAttribute.ValueOf(type) is { } actionId)
            {
                types[actionId] = type;
            }
        }

        return types;
    }

    public IEnumerable<Type> LoadAllActionTypes(IActionTypeLoaderContext context)
    {
        var asm = GetAssembly(context.Index);
        var assemblyTypes = GetTypesWithoutErrors(asm);

        var actionType = typeof(IAction);
        var actionTypeAttribute = typeof(ActionTypeAttribute);
        var types = new Dictionary<string, Type>();

        foreach (Type type in assemblyTypes)
        {
            if (type is null)
            {
                continue;
            }

            if (!type.IsAbstract &&
                actionType.IsAssignableFrom(type))
            {
                yield return type;
            }
        }
    }

    private Type[] GetTypesWithoutErrors(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray();
        }

        return types;
    }

    private Assembly GetAssembly(long blockIndex)
    {
        var pluginPath = Path.GetFullPath(Path.Join(_basePath, GetHardForkName(blockIndex)));
        var assemblyPath = Path.GetFullPath(Path.Join(pluginPath, _assemblyFileName));
        if (_cache.TryGetValue(assemblyPath, out Assembly value))
        {
            return value;
        }

        var context = new PluginLoadContext(assemblyPath);
        _cache[assemblyPath] =
            context.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(assemblyPath)));
        return _cache[assemblyPath];
    }

    private string GetHardForkName(long blockIndex)
    {
        for (int i = 0; i < _hardForks.Count - 1; ++i)
        {
            if (_hardForks[i].SinceBlockIndex <= blockIndex && blockIndex < _hardForks[i + 1].SinceBlockIndex)
            {
                return _hardForks[i].VersionName;
            }
        }

        return _hardForks[^1].VersionName;
    }
}
