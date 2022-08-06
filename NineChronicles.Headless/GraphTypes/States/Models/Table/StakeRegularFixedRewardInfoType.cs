using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularFixedRewardInfoType : ObjectGraphType<StakeRegularFixedRewardSheet.RewardInfo>
    {
        public StakeRegularFixedRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularFixedRewardSheet.RewardInfo.ItemId),
                resolve: context => context.Source.ItemId
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularFixedRewardSheet.RewardInfo.Count),
                resolve: context => context.Source.Count
            );
        }
    }
}
