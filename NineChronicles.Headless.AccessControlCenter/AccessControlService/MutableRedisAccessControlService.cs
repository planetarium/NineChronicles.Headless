using System.Collections.Generic;
using System.Linq;
using Libplanet.Crypto;
using NineChronicles.Headless.Services;

namespace NineChronicles.Headless.AccessControlCenter.AccessControlService
{
    public class MutableRedisAccessControlService : RedisAccessControlService, IMutableAccessControlService
    {
        public MutableRedisAccessControlService(string storageUri) : base(storageUri)
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
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            return server
                .Keys()
                .Select(k => new Address(k.ToString()))
                .Skip(offset)
                .Take(limit)
                .ToList();
        }
    }
}
