using System.Collections.Generic;
using System.Linq;
using Libplanet.Crypto;
using NineChronicles.Headless.Services;
using StackExchange.Redis;

namespace NineChronicles.Headless.AccessControlCenter.AccessControlService
{
    public class MutableRedisAccessControlService
        : RedisAccessControlService,
            IMutableAccessControlService
    {
        public MutableRedisAccessControlService(string storageUri)
            : base(storageUri)
        {
        }

        public void AddTxQuota(Address address, int quota)
        {
            _db.StringSet(address.ToString(), quota.ToString());
        }

        public void RemoveTxQuota(Address address)
        {
            _db.KeyDelete(address.ToString());
        }

        public List<Address> ListTxQuotaAddresses(int offset, int limit)
        {
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());

            var result = (RedisResult[]?)
                server.Execute("SCAN", offset.ToString(), "COUNT", limit.ToString());
            if (result != null)
            {
                RedisKey[] keys = (RedisKey[])result[1]!;
                return keys.Select(k => new Address(k.ToString())).ToList();
            }

            return new List<Address>();
        }
    }
}
