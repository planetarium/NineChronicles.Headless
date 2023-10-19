using StackExchange.Redis;
using Libplanet.Crypto;
using Nekoyume.Blockchain;
using Serilog;

namespace NineChronicles.Headless.Services
{
    public class RedisAccessControlService : IAccessControlService
    {
        protected IDatabase _db;

        public RedisAccessControlService(string storageUri)
        {
            var redis = ConnectionMultiplexer.Connect(storageUri);
            _db = redis.GetDatabase();
        }

        public bool IsAccessDenied(Address address)
        {
            var result = _db.KeyExists(address.ToString());
            if (result)
            {
                Log.Debug($"{address} is access denied");
            }

            return result;
        }
    }
}
