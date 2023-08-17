using GraphQL.Types;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class StakeRegularRewardInfoType : ObjectGraphType<StakeRegularRewardSheet.RewardInfo>
    {
        public StakeRegularRewardInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.ItemId),
                resolve: context => context.Source.ItemId
            );
            Field<NonNullGraphType<IntGraphType>>(
#pragma warning disable CS0618 // Type or member is obsolete
                nameof(StakeRegularRewardSheet.RewardInfo.Rate),
                resolve: context => context.Source.Rate
#pragma warning restore CS0618 // Type or member is obsolete
            );
            Field<NonNullGraphType<StakeRewardEnumType>>(
                nameof(StakeRegularRewardSheet.RewardInfo.Type),
                resolve: context => context.Source.Type);
        }
    }
}
