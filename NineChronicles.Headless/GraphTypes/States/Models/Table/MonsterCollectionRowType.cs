using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class MonsterCollectionRowType : ObjectGraphType<(MonsterCollectionSheet.Row row,
        MonsterCollectionRewardSheet monsterCollectionRewardSheet)>
    {
        public MonsterCollectionRowType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(MonsterCollectionSheet.Row.Level))
                .Resolve(context => context.Source.row.Level);
            Field<NonNullGraphType<IntGraphType>>(nameof(MonsterCollectionSheet.Row.RequiredGold))
                .Resolve(context => context.Source.row.RequiredGold);
            Field<NonNullGraphType<ListGraphType<MonsterCollectionRewardInfoType>>>(
                nameof(MonsterCollectionRewardSheet.Row.Rewards))
                .Resolve(context =>
                {
                    if (context.Source.monsterCollectionRewardSheet.ContainsKey(context.Source.row.Level))
                    {
                        return context.Source.monsterCollectionRewardSheet[context.Source.row.Level].Rewards;
                    }

                    return null;
                });
        }
    }
}
