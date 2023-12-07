using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;

namespace NineChronicles.Headless.Utils;

public static class ArenaParticipantsUtils
{
    private static readonly MessagePackSerializerOptions Lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    public static byte[] SetArenaParticipants(string cacheKey, List<ArenaParticipant> arenaParticipants)
    {
        var compressed = MessagePackSerializer.Serialize(arenaParticipants, Lz4Options);
        var filePath = GetFilePath(cacheKey);
        File.WriteAllBytes(filePath, compressed);
        return compressed;
    }

    public static List<ArenaParticipant> GetArenaParticipants(string cacheKey)
    {
        var filePath = GetFilePath(cacheKey);
        byte[] cached = File.ReadAllBytes(filePath);
        if (cached.Any())
        {
            return MessagePackSerializer.Deserialize<List<ArenaParticipant>>(cached, Lz4Options);
        }

        return new List<ArenaParticipant>();
    }

    public static string GetFilePath(string cacheKey)
    {
        var filePath = Path.Combine(Path.GetTempPath(), cacheKey);
        return filePath;
    }
}
