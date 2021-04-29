using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class StakingStatus
    {
        public bool CanReceive { get; }
        public FungibleAssetValue FungibleAssetValue { get; }

        public StakingStatus(bool canReceive, FungibleAssetValue fungibleAssetValue)
        {
            CanReceive = canReceive;
            FungibleAssetValue = fungibleAssetValue;
        }
    }
}
