using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    internal class DailyRewardStatusType : ObjectGraphType<DailyRewardStatus>
    {
        public DailyRewardStatusType()
        {
            Field<ListGraphType<LongGraphType>>(
                   nameof(DailyRewardStatus.lastRewardIndex),
                   resolve: context => context.Source.lastRewardIndex
                );
            Field<ListGraphType<IntGraphType>>(
                nameof(DailyRewardStatus.actionPoint),
                resolve: context => context.Source.actionPoint
                );
        }
    }
}
