using System.Collections.Generic;
using Libplanet.Assets;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatus
    {
        public FungibleAssetValue FungibleAssetValue { get; }

        public List<MonsterCollectionRewardSheet.RewardInfo> RewardInfos { get; }

        public MonsterCollectionStatus(FungibleAssetValue fungibleAssetValue, List<MonsterCollectionRewardSheet.RewardInfo> rewardInfos)
        {
            FungibleAssetValue = fungibleAssetValue;
            RewardInfos = rewardInfos;
        }
    }
}
