using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularRewardInfoType: ObjectGraphType<StakeRegularRewardSheet.RewardInfo>
    {
        public StakeRegularRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.ItemId),
                resolve: context => context.Source.ItemId
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Rate),
                resolve: context => context.Source.Rate
            );
        }
    }
}
