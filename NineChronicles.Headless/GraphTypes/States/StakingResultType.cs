using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakingResultType : ObjectGraphType<StakingResult>
    {
        public StakingResultType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(StakingResult.avatarAddress),
                resolve: context => context.Source.avatarAddress);

            Field<NonNullGraphType<ListGraphType<StakingRewardInfoType>>>(
                nameof(StakingResult.rewards),
                resolve: context => context.Source.rewards);
        }
    }
}
