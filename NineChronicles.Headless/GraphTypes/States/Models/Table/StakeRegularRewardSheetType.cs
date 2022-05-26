using System.Linq;
using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularRewardSheetType : ObjectGraphType<StakeRegularRewardSheet>
    {
        public StakeRegularRewardSheetType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StakeRegularRewardRowType>>>>(
                nameof(MonsterCollectionSheet.OrderedList),
                resolve: context => context.Source.OrderedList.ToList());
        }
    }
}
