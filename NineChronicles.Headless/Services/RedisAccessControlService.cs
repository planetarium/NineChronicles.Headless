using System;
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

        public int? GetTxQuota(Address address)
        {
            RedisValue result = _db.StringGet(address.ToString());
            return result.IsNull
                ? null
                : Convert.ToInt32(result);
        }
    }
}
