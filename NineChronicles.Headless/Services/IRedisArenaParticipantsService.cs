using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NineChronicles.Headless.Services;

public interface IRedisArenaParticipantsService
{
    Task<List<ArenaParticipant>> GetValueAsync(string key);
    Task SetValueAsync(string key, List<ArenaParticipant> value, TimeSpan? expiry = null);
}
