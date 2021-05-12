using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatus
    {
        public bool CanReceive { get; }
        public FungibleAssetValue FungibleAssetValue { get; }

        public MonsterCollectionStatus(bool canReceive, FungibleAssetValue fungibleAssetValue)
        {
            CanReceive = canReceive;
            FungibleAssetValue = fungibleAssetValue;
        }
    }
}
