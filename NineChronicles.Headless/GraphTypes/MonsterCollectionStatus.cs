using System.Collections.Generic;
using Libplanet.Assets;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatus
    {
        public bool CanReceive { get; }
        public FungibleAssetValue FungibleAssetValue { get; }

        public List<MonsterCollectionRewardSheet.RewardInfo> RewardInfos { get; }

        public MonsterCollectionStatus(bool canReceive, FungibleAssetValue fungibleAssetValue, List<MonsterCollectionRewardSheet.RewardInfo> rewardInfos)
        {
            CanReceive = canReceive;
            FungibleAssetValue = fungibleAssetValue;
            RewardInfos = rewardInfos;
        }
    }
}
