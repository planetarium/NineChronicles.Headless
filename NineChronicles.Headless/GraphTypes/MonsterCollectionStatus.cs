using System.Collections.Generic;
using Libplanet.Assets;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatus
    {
        public FungibleAssetValue FungibleAssetValue { get; }

        public List<MonsterCollectionRewardSheet.RewardInfo> RewardInfos { get; }

        public bool Lockup { get; }

        public MonsterCollectionStatus(
            FungibleAssetValue fungibleAssetValue, 
            List<MonsterCollectionRewardSheet.RewardInfo> rewardInfos,
            bool lockup
        )
        {
            FungibleAssetValue = fungibleAssetValue;
            RewardInfos = rewardInfos;
            Lockup = lockup;
        }
    }
}
