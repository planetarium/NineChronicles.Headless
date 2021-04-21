using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakingRewardInfoType: ObjectGraphType<StakingRewardSheet.RewardInfo>
    {
        public StakingRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakingRewardSheet.RewardInfo.ItemId),
                resolve: context => context.Source.ItemId
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakingRewardSheet.RewardInfo.Quantity),
                resolve: context => context.Source.Quantity
            );
        }
    }
}
