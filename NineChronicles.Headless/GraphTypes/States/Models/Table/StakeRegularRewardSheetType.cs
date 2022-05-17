using System.Linq;
using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularRewardSheetType : ObjectGraphType<StakeRegularRewardSheet>
    {
        public StakeRegularRewardSheetType()
        {
            Field<ListGraphType<StakeRegularRewardRowType>>(
                nameof(MonsterCollectionSheet.OrderedList),
                resolve: context => context.Source.OrderedList.ToList());
        }
    }
}
