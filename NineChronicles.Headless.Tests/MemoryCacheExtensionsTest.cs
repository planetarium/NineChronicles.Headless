using System;
using System.Net.Http;
using System.Text;
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
        var cache = new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
        {
            SizeLimit = null
        }));

        var sheets = TableSheetsImporter.ImportSheets();
        var tableName = nameof(ItemRequirementSheet);
        var csv = sheets[tableName];
        var cacheKey = Addresses.GetSheetAddress(tableName).ToString().Replace("\r\n", "\n");
        var value = (Text)csv;
        var compressed = MemoryCacheExtensions.NormalizedBytes(csv);
        cache.SetSheet(cacheKey, value, TimeSpan.FromMilliseconds(100));
        var expected = Encoding.UTF8.GetString(compressed);
        Assert.True(cache.TryGetValue(cacheKey, out byte[] cached));
        Assert.Equal(compressed, cached);
        Assert.Equal(expected, cache.GetSheet(cacheKey));
        await Task.Delay(100);
        Assert.False(cache.TryGetValue(cacheKey, out byte[] _));
    }

    [Fact]
    public void NormalizedBytes()
    {
        var csv = "a,b,c\n1,2,3\n4,5,6\n";
        var csv2 = "a,b,c\r\n1,2,3\r\n4,5,6\r\n   ";
        var expectedString = "a,b,c\n1,2,3\n4,5,6";
        var expectedBytes = Encoding.UTF8.GetBytes(expectedString);
        Assert.Equal(expectedString, Encoding.UTF8.GetString(MemoryCacheExtensions.NormalizedBytes(csv)));
        Assert.Equal(expectedString, Encoding.UTF8.GetString(MemoryCacheExtensions.NormalizedBytes(csv2)));
        Assert.Equal(expectedBytes, MemoryCacheExtensions.NormalizedBytes(csv));
        Assert.Equal(expectedBytes, MemoryCacheExtensions.NormalizedBytes(csv2));
    }
}
