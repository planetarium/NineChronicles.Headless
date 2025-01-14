using System.Collections.Generic;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Model.State;
using Nekoyume;
using Nekoyume.Model.Stake;
using Nekoyume.Module;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.Abstractions;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeStateType : ObjectGraphType<StakeStateType.StakeStateContext>
    {
        public class StakeStateContext : StateContext
        {
            public StakeStateContext(StakeState stakeState, Address address, IWorldState worldState, long blockIndex, StateMemoryCache stateMemoryCache)
                : base(worldState, blockIndex, stateMemoryCache)
            {
                StakeState = stakeState;
                Address = address;
            }

            public StakeState StakeState { get; }
            public Address Address { get; }
        }

        public StakeStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                "address",
                description: "The address of current state.",
                resolve: context => context.Source.Address);
            Field<NonNullGraphType<StringGraphType>>(
                "deposit",
                description: "The staked amount.",
                resolve: context => context.Source.WorldState.GetBalance(
                        context.Source.Address,
                        new GoldCurrencyState(
                            (Dictionary)context.Source.WorldState.GetLegacyState(GoldCurrencyState.Address)!).Currency)
                    .GetQuantityString(true));
            Field<NonNullGraphType<LongGraphType>>(
                "startedBlockIndex",
                description: "The block index the user started to stake.",
                resolve: context => context.Source.StakeState.StartedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "receivedBlockIndex",
                description: "The block index the user received rewards.",
                resolve: context => context.Source.StakeState.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "cancellableBlockIndex",
                description: "The block index the user can cancel the staking.",
                resolve: context => context.Source.StakeState.CancellableBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "claimableBlockIndex",
                description: "The block index the user can claim rewards.",
                resolve: context => context.Source.StakeState.ClaimableBlockIndex);
            Field<StakeAchievementsType>(
                nameof(LegacyStakeState.Achievements),
                description: "The staking achievements.",
                deprecationReason: "Since StakeStateV2, the achievement became removed.",
                resolve: _ => null);
            Field<NonNullGraphType<StakeRewardsType>>(
                "stakeRewards",
                resolve: context =>
                {
                    if (context.Source.StakeState.Contract is not { } contract)
                    {
                        return null;
                    }

                    IReadOnlyList<IValue?> values = context.Source.WorldState.GetLegacyStates(new[]
                    {
                        Addresses.GetSheetAddress(contract.StakeRegularFixedRewardSheetTableName),
                        Addresses.GetSheetAddress(contract.StakeRegularRewardSheetTableName),
                    });

                    if (!(values[0] is Text fsv && values[1] is Text sv))
                    {
                        throw new ExecutionError("Could not found stake rewards sheets");
                    }

                    StakeRegularFixedRewardSheet stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                    StakeRegularRewardSheet stakeRegularRewardSheet = new StakeRegularRewardSheet();
                    stakeRegularFixedRewardSheet.Set(fsv);
                    stakeRegularRewardSheet.Set(sv);

                    return (stakeRegularRewardSheet, stakeRegularFixedRewardSheet);
                }
            );
        }
    }
}
