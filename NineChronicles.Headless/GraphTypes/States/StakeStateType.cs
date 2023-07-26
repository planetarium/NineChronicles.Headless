using System;
using Bencodex.Types;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.World;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Mail;
using NineChronicles.Headless.GraphTypes.States.Models.Quest;
using Nekoyume.Blockchain.Policy;
using Nekoyume;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeStateType : ObjectGraphType<StakeStateType.StakeStateContext>
    {
        public class StakeStateContext : StateContext
        {
            public StakeStateContext(StakeState stakeState, IAccountState accountState, long blockIndex)
                : base(accountState, blockIndex)
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
                resolve: context => context.Source.AccountState.GetBalance(
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
                resolve: context => context.Source.StakeState.GetClaimableBlockIndex(
                    context.Source.BlockIndex));
            Field<NonNullGraphType<StakeAchievementsType>>(
                nameof(StakeState.Achievements),
                description: "The staking achievements.",
                resolve: context => context.Source.StakeState.Achievements);
        }
    }
}
