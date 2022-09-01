using System;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.World;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Mail;
using NineChronicles.Headless.GraphTypes.States.Models.Quest;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeStateType : ObjectGraphType<StakeStateType.StakeStateContext>
    {
        public class StakeStateContext : StateContext
        {
            public StakeStateContext(StakeState stakeState, AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter, long blockIndex)
                : base(accountStateGetter, accountBalanceGetter, blockIndex)
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
                resolve: context => context.Source.AccountBalanceGetter(
                        context.Source.StakeState.address,
                        new GoldCurrencyState((Dictionary)context.Source.GetState(GoldCurrencyState.Address)!).Currency)
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
                resolve: context =>
                {
                    var stakeState = context.Source.StakeState;

                    if (stakeState.ReceivedBlockIndex > 0)
                    {
                        long lastStep = Math.DivRem(
                            stakeState.ReceivedBlockIndex - stakeState.StartedBlockIndex,
                            StakeState.RewardInterval,
                            out _
                        );

                        return stakeState.StartedBlockIndex + (lastStep + 1) * StakeState.RewardInterval;
                    }

                    return stakeState.StartedBlockIndex + StakeState.RewardInterval;
                });
            Field<NonNullGraphType<StakeAchievementsType>>(
                nameof(StakeState.Achievements),
                description: "The staking achievements.",
                resolve: context => context.Source.StakeState.Achievements);
        }
    }
}
