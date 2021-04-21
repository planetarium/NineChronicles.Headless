using System.Linq;
using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakingSheetType : ObjectGraphType<(StakingSheet stakingSheet, StakingRewardSheet stakingRewardSheet)>
    {
        public StakingSheetType()
        {
            Field<ListGraphType<StakingRowType>>(
                nameof(StakingSheet.OrderedList),
                resolve: context =>
                {
                    return context.Source.stakingSheet.OrderedList
                        .Select(r => (r, context.Source.stakingRewardSheet))
                        .ToList();
                });
        }
    }
}
