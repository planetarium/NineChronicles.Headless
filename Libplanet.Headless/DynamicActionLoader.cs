using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Types.Blocks;
using Serilog;

using HardFork = Libplanet.Headless.DynamicActionTypeLoaderConfiguration.HardFork;

namespace Libplanet.Headless;

public class DynamicActionLoader : IActionLoader
{
    private readonly string _basePath;
    private readonly string _assemblyFileName;
    private readonly IImmutableList<HardFork> _hardForks;
    private readonly IDictionary<string, Assembly> _cache;

    public DynamicActionLoader(string basePath, string assemblyFileName, IOrderedEnumerable<HardFork> hardForks)
    {
        _basePath = basePath;
        _assemblyFileName = assemblyFileName;
        _hardForks = hardForks.ToImmutableList();
        _cache = new Dictionary<string, Assembly>();
    }

    public IDictionary<IValue, Type> Load(long blockIndex)
    {
        var types = new Dictionary<IValue, Type>();

        foreach (Type type in LoadAllActionTypes(blockIndex))
        {
            if (type.GetCustomAttribute<ActionTypeAttribute>() is { } attr)
            {
                types[attr.TypeIdentifier] = type;
            }
        }

        return types;
    }

    public IEnumerable<Type> LoadAllActionTypes(long blockIndex)
    {
        var asm = GetAssembly(blockIndex);
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

    public IAction LoadAction(long blockIndex, IValue plainValue)
    {
        if (plainValue is not Dictionary asDict)
        {
            throw new ArgumentException(
                "Given plainValue isn't bencodex Dictionary",
                nameof(plainValue)
            );
        }

        if (!asDict.TryGetValue((Text)"type_id", out IValue rawTypeId))
        {
            throw new ArgumentException(
                "Given plainValue doesn't have the type_id field",
                nameof(plainValue)
            );
        }

        if (rawTypeId is not Text typeId)
        {
            throw new ArgumentException(
                $"type_id value isn't bencodex Text.",
                nameof(plainValue)
            );
        }

        if (!Load(blockIndex).TryGetValue(typeId, out Type actionType))
        {
            throw new ArgumentException(
                $"There is no action type for {typeId} at #{blockIndex}",
                nameof(plainValue)
            );
        }

        var action = (IAction)Activator.CreateInstance(actionType);
        action.LoadPlainValue(plainValue);
        return action;
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
