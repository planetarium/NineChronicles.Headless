using System;
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

        public void DenyAccess(Address address)
        {
            _db.StringSet(address.ToString(), "denied");
        }

        public void AllowAccess(Address address)
        {
            _db.KeyDelete(address.ToString());
        }

        public List<Address> ListBlockedAddresses(int offset, int limit)
        {
            if (limit > 30)
            {
                throw new ArgumentException("Limit cannot exceed 30.", nameof(limit));
            }

            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());

            var result = (RedisResult[]?)
                server.Execute("SCAN", offset.ToString(), "COUNT", limit.ToString());
            if (result != null)
            {
                long newCursor = long.Parse((string)result[0]!);
                RedisKey[] keys = (RedisKey[])result[1]!;

                return keys.Select(k => new Address(k.ToString())).ToList();
            }
            return new List<Address>();
        }
    }
}