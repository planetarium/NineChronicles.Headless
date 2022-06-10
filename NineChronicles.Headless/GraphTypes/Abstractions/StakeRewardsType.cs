using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.Abstractions
{
    public class StakeRewardsType : ObjectGraphType<(StakeRegularRewardSheet StakeRegularRewardSheet, StakeRegularFixedRewardSheet StakeRegularFixedRewardSheet)>
    {
        public StakeRewardsType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StakeRegularRewardsType>>>>(
                nameof(MonsterCollectionSheet.OrderedList),
                resolve: context =>
                {
                    var rows =
                        new List<(
                            int Level,
                            long RequiredGold,
                            StakeRegularRewardSheet.RewardInfo[] Rewards,
                            StakeRegularFixedRewardSheet.RewardInfo[] BonusRewards
                        )>();
                    foreach (var x in context.Source.StakeRegularRewardSheet.OrderedRows
                                 .Concat(context.Source.StakeRegularFixedRewardSheet.OrderedRows).GroupBy(x =>
                                     (x.Level, x.RequiredGold)))
                    {
                        var rewards = x.Where(row => row is StakeRegularRewardSheet.Row)
                            .Cast<StakeRegularRewardSheet.Row>().SelectMany(row => row.Rewards).ToArray();
                        var bonusRewards = x.Where(row => row is StakeRegularFixedRewardSheet.Row)
                            .Cast<StakeRegularFixedRewardSheet.Row>().SelectMany(row => row.Rewards).ToArray();
                        rows.Add((x.Key.Level, x.Key.RequiredGold, rewards, bonusRewards));
                    }

                    return rows.OrderBy(row => row.Level);
                });
        }
    }
}
