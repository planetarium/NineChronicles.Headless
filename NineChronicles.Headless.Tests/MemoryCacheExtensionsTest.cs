using System;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using MessagePack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nekoyume;
using Nekoyume.TableData;
using Xunit;

namespace NineChronicles.Headless.Tests;

public class MemoryCacheExtensionsTest
{
    [Fact]
    public async Task Sheet()
    {
        var codec = new Codec();
        var lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        var cache = new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
        {
            SizeLimit = null
        }));

        var sheets = TableSheetsImporter.ImportSheets();
        var tableName = nameof(ItemRequirementSheet);
        var csv = sheets[tableName];
        var cacheKey = Addresses.GetSheetAddress(tableName).ToString();
        var value = (Text)csv;
        var compressed = MessagePackSerializer.Serialize(codec.Encode(value), lz4Options);
        cache.SetSheet(cacheKey, value, TimeSpan.FromMilliseconds(100));
        Assert.True(cache.TryGetValue(cacheKey, out byte[] cached));
        Assert.Equal(compressed, cached);
        Assert.Equal(csv, cache.GetSheet(cacheKey));
        await Task.Delay(100);
        Assert.False(cache.TryGetValue(cacheKey, out byte[] _));
    }
}
