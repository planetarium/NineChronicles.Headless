using System;
using StackExchange.Redis;
using Libplanet.Crypto;
using NineChronicles.Headless.AccessControlService;
using Serilog;

namespace NineChronicles.Headless.Services
{
    public class RedisAccessControlService : IAccessControlService
    {
        protected IDatabase _db;

        public RedisAccessControlService(string storageUri)
        {
            var configurationOptions = new ConfigurationOptions
            {
                EndPoints = { storageUri },
                ConnectTimeout = 500,
                SyncTimeout = 500,
            };

            var redis = ConnectionMultiplexer.Connect(configurationOptions);
            _db = redis.GetDatabase();
        }

        public int? GetTxQuota(Address address)
        {
            RedisValue result = _db.StringGet(address.ToString());
            if (!result.IsNull)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Debug("\"{Address}\" Tx Quota: {Quota}", address, result);
                return Convert.ToInt32(result);
            }

            return null;
        }
    }
}
