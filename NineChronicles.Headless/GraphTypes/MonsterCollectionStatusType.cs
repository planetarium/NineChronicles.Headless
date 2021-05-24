using GraphQL.Types;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatusType : ObjectGraphType<MonsterCollectionStatus>
    {
        public MonsterCollectionStatusType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(MonsterCollectionStatus.CanReceive),
                resolve: context => context.Source.CanReceive
            );
            Field<NonNullGraphType<FungibleAssetValueType>>(
                nameof(MonsterCollectionStatus.FungibleAssetValue),
                resolve: context => context.Source.FungibleAssetValue
            );
            Field<ListGraphType<MonsterCollectionRewardInfoType>>(
                nameof(MonsterCollectionStatus.RewardInfos),
                resolve: context => context.Source.RewardInfos
            );
        }
    }
}
