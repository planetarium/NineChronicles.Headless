using System;
using System.Threading.Tasks;
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
                AbortOnConnectFail = false,
            };

            var redis = ConnectionMultiplexer.Connect(configurationOptions);
            _db = redis.GetDatabase();
        }

        public async Task<int?> GetTxQuotaAsync(Address address)
        {
            try
            {
                RedisValue result = await _db.StringGetAsync(address.ToString());
                return !result.IsNull ? Convert.ToInt32(result) : null;
            }
            catch (RedisTimeoutException ex)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Error(ex, "\"{Address}\" Redis timeout encountered.", address);
                return null;
            }
        }
    }
}
