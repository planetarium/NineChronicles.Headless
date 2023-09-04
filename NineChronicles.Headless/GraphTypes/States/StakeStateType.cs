using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Nekoyume.Model.State;
using Nekoyume.Module;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeStateType : ObjectGraphType<StakeStateType.StakeStateContext>
    {
        public class StakeStateContext : StateContext
        {
            public StakeStateContext(StakeState stakeState, IWorldState worldState, long blockIndex)
                : base(worldState, blockIndex)
            {
                StakeState = stakeState;
            }

            public StakeState StakeState { get; }
        }

        public StakeStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(StakeState.address),
                description: "The address of current state.",
                resolve: context => context.Source.StakeState.address);
            Field<NonNullGraphType<StringGraphType>>(
                "deposit",
                description: "The staked amount.",
                resolve: context => LegacyModule.GetBalance(
                        context.Source.WorldState,
                        context.Source.StakeState.address,
                        new GoldCurrencyState(
                                (Dictionary)LegacyModule.GetState(
                                    context.Source.WorldState,
                                    GoldCurrencyState.Address)!)
                            .Currency)
                    .GetQuantityString(true));
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeState.StartedBlockIndex),
                description: "The block index the user started to stake.",
                resolve: context => context.Source.StakeState.StartedBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(StakeState.ReceivedBlockIndex),
                description: "The block index the user received rewards.",
                resolve: context => context.Source.StakeState.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(StakeState.CancellableBlockIndex),
                description: "The block index the user can cancel the staking.",
                resolve: context => context.Source.StakeState.CancellableBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "claimableBlockIndex",
                description: "The block index the user can claim rewards.",
                resolve: context => context.Source.StakeState.GetClaimableBlockIndex(
                    context.Source.BlockIndex));
            Field<NonNullGraphType<StakeAchievementsType>>(
                nameof(StakeState.Achievements),
                description: "The staking achievements.",
                resolve: context => context.Source.StakeState.Achievements);
        }
    }
}
