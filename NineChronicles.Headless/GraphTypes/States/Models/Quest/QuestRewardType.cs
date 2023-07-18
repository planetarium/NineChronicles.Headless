using System.Linq;
using GraphQL.Types;
using Nekoyume.Model.Quest;

namespace NineChronicles.Headless.GraphTypes.States.Models.Quest
{
    public class QuestRewardType : ObjectGraphType<QuestReward>
    {
        public QuestRewardType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ListGraphType<NonNullGraphType<IntGraphType>>>>>>(
                nameof(QuestReward.ItemMap),
                resolve: context => context.Source.ItemMap.Select(pair => new[] { pair.Item1, pair.Item2 }));
        }
    }
}
