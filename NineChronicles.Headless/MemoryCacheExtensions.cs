using System;
using Bencodex;
using Bencodex.Types;
using MessagePack;
using Microsoft.Extensions.Caching.Memory;

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
        return cache.TryGetValue(cacheKey, out cached!);
    }

    public static string? GetSheet(this MemoryCache cache, string cacheKey)
    {
        if (cache.TryGetSheet(cacheKey, out byte[] cached))
        {
            return (Text)Codec.Decode(MessagePackSerializer.Deserialize<byte[]>(cached, Lz4Options));
        }

        return null;
    }
}
