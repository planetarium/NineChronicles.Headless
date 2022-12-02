using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class MonsterCollectionRewardInfoType : ObjectGraphType<MonsterCollectionRewardSheet.RewardInfo>
    {
        public MonsterCollectionRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(MonsterCollectionRewardSheet.RewardInfo.ItemId))
                .Resolve(context => context.Source.ItemId);
            Field<NonNullGraphType<IntGraphType>>(nameof(MonsterCollectionRewardSheet.RewardInfo.Quantity))
                .Resolve(context => context.Source.Quantity);
        }
    }
}
