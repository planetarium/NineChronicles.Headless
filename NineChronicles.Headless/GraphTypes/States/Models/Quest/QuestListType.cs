using GraphQL.Types;
using Nekoyume.Model.Quest;

namespace NineChronicles.Headless.GraphTypes.States.Models.Quest
{
    public class QuestListType : ObjectGraphType<QuestList>
    {
        public QuestListType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<IntGraphType>>>>(
                nameof(QuestList.completedQuestIds),
                resolve: context => context.Source.completedQuestIds);
        }
    }
}
