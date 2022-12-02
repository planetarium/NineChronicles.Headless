using GraphQL.Types;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes.Abstractions
{
    public class StakeRegularRewardsType : ObjectGraphType<(int Level, long RequiredGold, StakeRegularRewardSheet.RewardInfo[] Rewards, StakeRegularFixedRewardSheet.RewardInfo[] BonusRewards)>
    {
        public StakeRegularRewardsType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(StakeRegularRewardSheet.Row.Level))
                .Resolve(context => context.Source.Level);
            Field<NonNullGraphType<LongGraphType>>(nameof(StakeRegularRewardSheet.Row.RequiredGold))
                .Resolve(context => context.Source.RequiredGold);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StakeRegularRewardInfoType>>>>(
                nameof(StakeRegularRewardSheet.Row.Rewards))
                .Resolve(context => context.Source.Rewards);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StakeRegularFixedRewardInfoType>>>>(
                "bonusRewards")
                .Resolve(context => context.Source.BonusRewards);
        }
    }
}
