using Libplanet.Crypto;
using System.Collections.Generic;
using NineChronicles.Headless.AccessControlService;

namespace NineChronicles.Headless.AccessControlCenter.AccessControlService
{
    public interface IMutableAccessControlService : IAccessControlService
    {
        void AddTxQuota(Address address, int quota);
        void RemoveTxQuota(Address address);
        List<Address> ListTxQuotaAddresses(int offset, int limit);
    }
}
