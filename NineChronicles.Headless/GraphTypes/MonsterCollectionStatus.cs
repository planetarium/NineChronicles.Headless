using System.Collections.Generic;
using Libplanet.Types.Assets;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatus
    {
        public FungibleAssetValue FungibleAssetValue { get; }

        public List<MonsterCollectionRewardSheet.RewardInfo> RewardInfos { get; }

        public bool Lockup { get; }

        public long TipIndex { get; }

        public MonsterCollectionStatus(FungibleAssetValue fungibleAssetValue,
            List<MonsterCollectionRewardSheet.RewardInfo> rewardInfos,
            long tipIndex,
            bool lockup)
        {
            FungibleAssetValue = fungibleAssetValue;
            RewardInfos = rewardInfos;
            TipIndex = tipIndex;
            Lockup = lockup;
        }
    }
}
