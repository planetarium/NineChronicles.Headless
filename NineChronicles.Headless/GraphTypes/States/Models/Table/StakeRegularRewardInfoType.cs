using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularRewardInfoType : ObjectGraphType<StakeRegularRewardSheet.RewardInfo>
    {
        public StakeRegularRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.ItemId))
                .Resolve(context => context.Source.ItemId);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Rate))
                .Resolve(context => context.Source.Rate);
            Field<NonNullGraphType<StakeRewardEnumType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Type))
                .Resolve(context => context.Source.Type);
        }
    }
}
