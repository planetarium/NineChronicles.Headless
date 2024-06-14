using System;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action.State;
using MessagePack;
using Microsoft.Extensions.Caching.Memory;
using Nekoyume;
using Nekoyume.Module;
using Nekoyume.TableData;

namespace NineChronicles.Headless;

public static class MemoryCacheExtensions
{
    private static readonly Codec Codec = new Codec();
    private static readonly MessagePackSerializerOptions Lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    public static byte[] SetSheet(this MemoryCache cache, string cacheKey, IValue value, TimeSpan ex)
    {
        var compressed = MessagePackSerializer.Serialize(Codec.Encode(value), Lz4Options);
        cache.Set(cacheKey, compressed, ex);
        return compressed;
    }

    public static bool TryGetSheet<T>(this MemoryCache cache, string cacheKey, out T cached)
    {
        return cache.TryGetValue(cacheKey, out cached);
    }

    public static string? GetSheet(this MemoryCache cache, string cacheKey)
    {
        if (cache.TryGetSheet(cacheKey, out byte[] cached))
        {
            return (Text)Codec.Decode(MessagePackSerializer.Deserialize<byte[]>(cached, Lz4Options));
        }

        return null;
    }

    public static T GetSheet<T>(this MemoryCache cache, IWorldState worldState) where T : ISheet, new()
    {
        var cacheKey = Addresses.GetSheetAddress<T>().ToString();
        var sheet = new T();
        var csv = string.Empty;
        if (cache.GetSheet(cacheKey) is { } s)
        {
            csv = s;
        }
        else
        {
            IValue value = Null.Value;
            if (worldState.GetSheetCsv<T>() is { } s2)
            {
                csv = s2;
                value = (Text)csv;
            }
            cache.SetSheet(cacheKey, value, TimeSpan.FromMinutes(1));
        }

        sheet.Set(csv);
        return sheet;
    }
}
