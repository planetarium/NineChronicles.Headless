using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    internal class DailyRewardStatusType : ObjectGraphType<DailyRewardStatus>
    {
        public DailyRewardStatusType()
        {
            Field<NonNullGraphType<LongGraphType>>(
                   nameof(DailyRewardStatus.lastRewardIndex),
                   resolve: context => context.Source.lastRewardIndex
                );
            Field<NonNullGraphType<IntGraphType>>(
                nameof(DailyRewardStatus.actionPoint),
                resolve: context => context.Source.actionPoint
                );
            Field<NonNullGraphType<AddressType>>(
                nameof(DailyRewardStatus.avatarAddress),
                resolve: context => context.Source.avatarAddress
                );
        }
    }
}
