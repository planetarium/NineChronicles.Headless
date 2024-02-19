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
            try
            {
                RedisValue result = _db.StringGet(address.ToString());

                return !result.IsNull ? Convert.ToInt32(result) : null;
            }
            catch (RedisTimeoutException)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Error("\"{Address}\" Redis timeout.", address);
                return null;
            }
        }
    }
}
