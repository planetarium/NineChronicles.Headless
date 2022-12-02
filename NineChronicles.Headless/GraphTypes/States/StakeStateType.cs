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
using Nekoyume.BlockChain.Policy;

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
            Field<NonNullGraphType<AddressType>>(nameof(StakeState.address))
                .Description("The address of current state.")
                .Resolve(context => context.Source.StakeState.address);
            Field<NonNullGraphType<StringGraphType>>("deposit")
                .Description("The staked amount.")
                .Resolve(context =>
                    context.Source.AccountBalanceGetter(
                        context.Source.StakeState.address,
                        new GoldCurrencyState((Dictionary)context.Source.GetState(GoldCurrencyState.Address)!).Currency)
                    .GetQuantityString(true));
            Field<NonNullGraphType<IntGraphType>>(nameof(StakeState.StartedBlockIndex))
                .Description("The block index the user started to stake.")
                .Resolve(context => context.Source.StakeState.StartedBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(nameof(StakeState.ReceivedBlockIndex))
                .Description("The block index the user received rewards.")
                .Resolve(context => context.Source.StakeState.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(StakeState.CancellableBlockIndex))
                .Description("The block index the user can cancel the staking.")
                .Resolve(context => context.Source.StakeState.CancellableBlockIndex);
            Field<NonNullGraphType<LongGraphType>>("claimableBlockIndex")
                .Description("The block index the user can claim rewards.")
                .Resolve(context =>
                {
                    var stakeState = context.Source.StakeState;
                    if (context.Source.BlockIndex >= BlockPolicySource.V100290ObsoleteIndex)
                    {
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
                    }

                    return Math.Max(stakeState.StartedBlockIndex, stakeState.ReceivedBlockIndex) + StakeState.RewardInterval;
                });
            Field<NonNullGraphType<StakeAchievementsType>>(nameof(StakeState.Achievements))
                .Description("The staking achievements.")
                .Resolve(context => context.Source.StakeState.Achievements);
        }
    }
}
