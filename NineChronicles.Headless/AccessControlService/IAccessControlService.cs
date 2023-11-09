using Libplanet.Crypto;

namespace NineChronicles.Headless.AccessControlService
{
    public interface IAccessControlService
    {
        public int? GetTxQuota(Address address);
    }
}
