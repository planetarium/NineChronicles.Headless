using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NineChronicles.Headless.Services;

public class RedisArenaParticipantsService : IRedisArenaParticipantsService
{
    private readonly IDatabase _db;

    public RedisArenaParticipantsService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<List<ArenaParticipant>> GetValueAsync(string key)
    {
        RedisValue result = await _db.StringGetAsync(key);
        if (result.IsNull)
        {
            return new List<ArenaParticipant>();
        }

        return JsonSerializer.Deserialize<List<ArenaParticipant>>(result.ToString())!;
    }

    public async Task SetValueAsync(string key, List<ArenaParticipant> value, TimeSpan? expiry = null)
    {
        var serialized = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, serialized, expiry);
    }
}
