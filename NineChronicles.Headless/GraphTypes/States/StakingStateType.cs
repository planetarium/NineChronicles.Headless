using System.Linq;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakingStateType : ObjectGraphType<StakingState>
    {
        public StakingStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(StakingState.address),
                resolve: context => context.Source.address);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakingState.Level),
                resolve: context => context.Source.Level);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakingState.ExpiredBlockIndex),
                resolve: context => context.Source.ExpiredBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakingState.StartedBlockIndex),
                resolve: context => context.Source.StartedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakingState.ReceivedBlockIndex),
                resolve: context => context.Source.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakingState.RewardLevel),
                resolve: context => context.Source.RewardLevel);
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(StakingState.End),
                resolve: context => context.Source.End);
            Field<NonNullGraphType<ListGraphType<StakingResultType>>>(
                nameof(StakingState.RewardMap),
                resolve: context => context.Source.RewardMap.Select(kv => kv.Value));
            Field<ListGraphType<ListGraphType<StakingRewardInfoType>>>(
                nameof(StakingState.RewardLevelMap),
                resolve: context =>
                {
                    return context.Source.RewardLevelMap.Select(kv => kv.Value).ToList();
                });
        }
    }
}
