using System.Linq;
using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class MonsterCollectionSheetType : ObjectGraphType<(MonsterCollectionSheet monsterCollectionSheet,
        MonsterCollectionRewardSheet monsterCollectionRewardSheet)>
    {
        public MonsterCollectionSheetType()
        {
            Field<ListGraphType<MonsterCollectionRowType>>(
                nameof(MonsterCollectionSheet.OrderedList),
                resolve: context =>
                {
                    return context.Source.monsterCollectionSheet.OrderedList?
                        .Select(r => (r, context.Source.monsterCollectionRewardSheet))
                        .ToList();
                });
        }
    }
}
