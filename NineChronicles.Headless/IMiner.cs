using Libplanet.Crypto;

namespace NineChronicles.Headless
{
    public interface IMiner
    {
        public PrivateKey? PrivateKey { get; set; }

        public void StartMining();

        public void StopMining();
    }
}
