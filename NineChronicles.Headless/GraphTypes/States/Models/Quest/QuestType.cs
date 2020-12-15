using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States.Models.Quest
{
    public class QuestType : ObjectGraphType<Nekoyume.Model.Quest.Quest>
    {
        public QuestType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(Nekoyume.Model.Quest.Quest.Id));
            Field<NonNullGraphType<IntGraphType>>(nameof(Nekoyume.Model.Quest.Quest.Goal));
            Field<NonNullGraphType<QuestRewardType>>(nameof(Nekoyume.Model.Quest.Quest.Reward));
            Field<NonNullGraphType<BooleanGraphType>>(nameof(Nekoyume.Model.Quest.Quest.Complete));
            Field<NonNullGraphType<BooleanGraphType>>(nameof(Nekoyume.Model.Quest.Quest.IsPaidInAction));
        }
    }
}
