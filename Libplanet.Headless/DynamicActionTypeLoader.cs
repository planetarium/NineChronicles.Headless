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

    public DynamicActionTypeLoader(string basePath, string assemblyFileName, IOrderedEnumerable<HardFork> hardForks)
    {
        _basePath = basePath;
        _assemblyFileName = assemblyFileName;
        _hardForks = hardForks.ToImmutableList();
    }

    public IDictionary<string, Type> Load(IPreEvaluationBlockHeader blockHeader)
    {
        return Load(blockHeader.Index);
    }

    public IDictionary<string, Type> Load(long index)
    {
        var asm = GetAssembly(index);
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
                actionType.IsAssignableFrom(type) &&
                ActionTypeAttribute.ValueOf(type) is { } actionId)
            {
                types[actionId] = type;
            }
        }

        return types;
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
        Console.WriteLine("assemblyPath " + assemblyPath);
        var context = new PluginLoadContext(assemblyPath);
        Log.Debug("Context: {context}", context);
        return context.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(assemblyPath)));
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
