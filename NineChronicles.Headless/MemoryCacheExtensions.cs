using System;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace NineChronicles.Headless;

public static class MemoryCacheExtensions
{

    public static byte[] SetSheet(this MemoryCache cache, string cacheKey, string value, TimeSpan ex)
    {
        var normalized = NormalizedBytes(value);
        cache.Set(cacheKey, normalized, ex);
        return normalized;
    }

    public static bool TryGetSheet<T>(this MemoryCache cache, string cacheKey, out T? result)
    {
        if (cache.TryGetValue(cacheKey, out T? cached))
        {
            result = cached;
            return true;
        }

        result = default!;
        return false;
    }

    public static string? GetSheet(this MemoryCache cache, string cacheKey)
    {
        if (cache.TryGetSheet(cacheKey, out byte[]? cached))
        {
            return Encoding.UTF8.GetString(cached!);
        }

        return null;
    }

    public static byte[] NormalizedBytes(string csvText)
    {
        // Normalize line endings
        csvText = csvText.Replace("\r\n", "\n");

        // Remove empty lines at the end
        csvText = csvText.Trim();

        // Unify encoding (e.g. UTF-8)
        return Encoding.UTF8.GetBytes(csvText);
    }
}
