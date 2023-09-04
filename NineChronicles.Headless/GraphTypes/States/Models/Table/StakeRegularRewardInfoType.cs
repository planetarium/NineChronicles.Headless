using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularRewardInfoType : ObjectGraphType<StakeRegularRewardSheet.RewardInfo>
    {
        public StakeRegularRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.ItemId),
                resolve: context => context.Source.ItemId
            );
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.DecimalRate),
                resolve: context => context.Source.DecimalRate
            );
            Field<NonNullGraphType<StakeRewardEnumType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Type),
                resolve: context => context.Source.Type);
        }
    }
}
