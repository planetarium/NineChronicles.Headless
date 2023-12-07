using System.Collections.Generic;
using System.IO;
using Lib9c.Formatters;
using MessagePack;
using MessagePack.Resolvers;
using NineChronicles.Headless.Utils;
using Xunit;

namespace NineChronicles.Headless.Tests;

public class ArenaParticipantsUtilsTest
{
    private readonly MessagePackSerializerOptions _lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    [Fact]
    public void ArenaParticipants()
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
        var cacheKey = Path.GetTempFileName();
        var filePath = ArenaParticipantsUtils.GetFilePath(cacheKey);
        Assert.Empty(ArenaParticipantsUtils.GetArenaParticipants(cacheKey));

        var cached = ArenaParticipantsUtils.SetArenaParticipants(cacheKey, result);
        Assert.Equal(compressed, cached);
        Assert.True(File.Exists(filePath));
        Assert.NotEmpty(ArenaParticipantsUtils.GetArenaParticipants(cacheKey));

        ArenaParticipantsUtils.SetArenaParticipants(cacheKey, new List<ArenaParticipant>());
        Assert.True(File.Exists(filePath));
        Assert.Empty(ArenaParticipantsUtils.GetArenaParticipants(cacheKey));
    }
}
