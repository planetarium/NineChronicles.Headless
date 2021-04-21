using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakingRowType : ObjectGraphType<(StakingSheet.Row stakingRow, StakingRewardSheet stakingRewardSheet)>
    {
        public StakingRowType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakingSheet.Row.Level),
                resolve: context => context.Source.stakingRow.Level
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakingSheet.Row.RequiredGold),
                resolve: context => context.Source.stakingRow.RequiredGold
            );
            Field<NonNullGraphType<ListGraphType<StakingRewardInfoType>>>(
                nameof(StakingRewardSheet.Row.Rewards),
                resolve: context =>
                {
                    if (context.Source.stakingRewardSheet.ContainsKey(context.Source.stakingRow.Level))
                    {
                        return context.Source.stakingRewardSheet[context.Source.stakingRow.Level].Rewards;
                    }

                    return null;
                }
            );
        }
    }
}
