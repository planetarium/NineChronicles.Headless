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

        public bool IsAccessDenied(Address address)
        {
            var result = _db.KeyExists(address.ToString());
            if (result)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Debug("\"{Address}\" is access denied", address);
            }

            return result;
        }

        public int GetAccessLevel(Address address)
        {
            RedisValue result = _db.StringGet(address.ToString());
            if (result.IsNull)
            {
                result = "-1";
            }
            else
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Debug("\"{Address}\" access level: {level}", address, result);
            }

            return Convert.ToInt32(result);
        }
    }
}
