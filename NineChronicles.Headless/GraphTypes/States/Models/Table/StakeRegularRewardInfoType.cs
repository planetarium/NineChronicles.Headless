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
#pragma warning disable CS0618
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Rate),
                resolve: context => context.Source.Rate
            );
#pragma warning restore CS0618
            Field<NonNullGraphType<StakeRewardEnumType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Type),
                resolve: context => context.Source.Type
            );
            Field<StringGraphType>(
                nameof(StakeRegularRewardSheet.RewardInfo.CurrencyTicker),
                resolve: context => context.Source.CurrencyTicker
            );
            Field<IntGraphType>(
                nameof(StakeRegularRewardSheet.RewardInfo.CurrencyDecimalPlaces),
                resolve: context => context.Source.CurrencyDecimalPlaces
            );
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.DecimalRate),
                resolve: context => context.Source.DecimalRate
            );
        }
    }
}
