using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class StakingStatusType : ObjectGraphType<StakingStatus>
    {
        public StakingStatusType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(StakingStatus.CanReceive),
                resolve: context => context.Source.CanReceive
            );
            Field<NonNullGraphType<FungibleAssetValueType>>(
                nameof(StakingStatus.FungibleAssetValue),
                resolve: context => context.Source.FungibleAssetValue
            );
        }
    }
}
