using GraphQL.Types;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes
{
    public class MonsterCollectionStatusType : ObjectGraphType<MonsterCollectionStatus>
    {
        public MonsterCollectionStatusType()
        {
            Field<NonNullGraphType<FungibleAssetValueType>>(
                nameof(MonsterCollectionStatus.FungibleAssetValue))
                .Resolve(context => context.Source.FungibleAssetValue);
            Field<ListGraphType<MonsterCollectionRewardInfoType>>(
                nameof(MonsterCollectionStatus.RewardInfos))
                .Resolve(context => context.Source.RewardInfos);
            Field<NonNullGraphType<LongGraphType>>(nameof(MonsterCollectionStatus.TipIndex))
                .Resolve(context => context.Source.TipIndex);
            Field<NonNullGraphType<BooleanGraphType>>(nameof(MonsterCollectionStatus.Lockup))
                .Resolve(context => context.Source.Lockup);
        }
    }
}
