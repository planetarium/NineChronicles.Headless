using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class MonsterCollectionRowType : ObjectGraphType<(MonsterCollectionSheet.Row row,
        MonsterCollectionRewardSheet monsterCollectionRewardSheet)>
    {
        public MonsterCollectionRowType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(MonsterCollectionSheet.Row.Level),
                resolve: context => context.Source.row.Level
            );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(MonsterCollectionSheet.Row.RequiredGold),
                resolve: context => context.Source.row.RequiredGold
            );
            Field<NonNullGraphType<ListGraphType<MonsterCollectionRewardInfoType>>>(
                nameof(MonsterCollectionRewardSheet.Row.Rewards),
                resolve: context =>
                {
                    if (context.Source.monsterCollectionRewardSheet.ContainsKey(context.Source.row.Level))
                    {
                        return context.Source.monsterCollectionRewardSheet[context.Source.row.Level].Rewards;
                    }

                    return null;
                }
            );
        }
    }
}
