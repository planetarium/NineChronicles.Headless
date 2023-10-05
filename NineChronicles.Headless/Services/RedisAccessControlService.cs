using StackExchange.Redis;
using Libplanet.Crypto;
using Nekoyume.Blockchain;

namespace NineChronicles.Headless.Services
{
    public class RedisAccessControlService : IAccessControlService
    {
        private IDatabase _db;

        public RedisAccessControlService(string storageUri)
        {
            var redis = ConnectionMultiplexer.Connect(storageUri);
            _db = redis.GetDatabase();
        }

        public bool IsAccessDenied(Address address)
        {
            return _db.KeyExists(address.ToString());
        }
    }
}
