using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Lib9c.Formatters;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nekoyume;
using Nekoyume.TableData;
using Xunit;

namespace NineChronicles.Headless.Tests;

public class MemoryCacheExtensionsTest
{
    private readonly MessagePackSerializerOptions _lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    [Fact]
    public async Task Sheet()
    {
        var codec = new Codec();
        var cache = new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
        {
            SizeLimit = null
        }));

        var sheets = TableSheetsImporter.ImportSheets();
        var tableName = nameof(ItemRequirementSheet);
        var csv = sheets[tableName];
        var cacheKey = Addresses.GetSheetAddress(tableName).ToString();
        var value = (Text)csv;
        var compressed = MessagePackSerializer.Serialize(codec.Encode(value), _lz4Options);
        cache.SetSheet(cacheKey, value, TimeSpan.FromMilliseconds(100));
        Assert.True(cache.TryGetValue(cacheKey, out byte[] cached));
        Assert.Equal(compressed, cached);
        Assert.Equal(csv, cache.GetSheet(cacheKey));
        await Task.Delay(100);
        Assert.False(cache.TryGetValue(cacheKey, out byte[] _));
    }

    [Fact]
    public async Task ArenaParticipants()
    {
        var avatarState = Fixtures.AvatarStateFX;
        var resolver = MessagePack.Resolvers.CompositeResolver.Create(
            NineChroniclesResolver.Instance,
            StandardResolver.Instance
        );
        var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
        MessagePackSerializer.DefaultOptions = options;
        var ap = new ArenaParticipant(default, 1, 1, avatarState.NameWithHash, avatarState.level, 1, 1, 1, 1);
        var result = new List<ArenaParticipant>
        {
            ap
        };
        var compressed = MessagePackSerializer.Serialize(result, _lz4Options);
        var cache = new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions
        {
            SizeLimit = null
        }));
        var cacheKey = "0_0";
        cache.SetArenaParticipants(cacheKey, result, TimeSpan.FromMilliseconds(100));
        Assert.True(cache.TryGetValue(cacheKey, out byte[] cached));
        Assert.Equal(compressed, cached);
        Assert.NotEmpty(cache.GetArenaParticipants(cacheKey));
        await Task.Delay(100);
        Assert.False(cache.TryGetValue(cacheKey, out byte[] _));
        Assert.Empty(cache.GetArenaParticipants(cacheKey));
    }
}
