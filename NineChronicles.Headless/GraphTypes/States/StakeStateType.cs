using System;
using System.Collections.Generic;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.World;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Mail;
using NineChronicles.Headless.GraphTypes.States.Models.Quest;
using Nekoyume.Blockchain.Policy;
using Nekoyume;
using Nekoyume.Model.Stake;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.Abstractions;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeStateType : ObjectGraphType<StakeStateType.StakeStateContext>
    {
        public class StakeStateContext : StateContext
        {
            public class StakeStateWrapper
            {
                private readonly StakeState? _v1;
                private readonly StakeStateV2? _v2;
                private readonly Address? _v2Address;

                public StakeStateWrapper(StakeState stakeState)
                {
                    _v1 = stakeState;
                    _v2 = null;
                    _v2Address = null;
                }

                public StakeStateWrapper(StakeStateV2 stakeStateV2, Address stakeStateV2Address)
                {
                    _v1 = null;
                    _v2 = stakeStateV2;
                    _v2Address = stakeStateV2Address;
                }

                public long StartedBlockIndex => _v1?.StartedBlockIndex ??
                                                 _v2?.StartedBlockIndex ?? throw new InvalidOperationException();

                public long ReceivedBlockIndex => _v1?.ReceivedBlockIndex ??
                                                  _v2?.ReceivedBlockIndex ?? throw new InvalidOperationException();

                public long GetClaimableBlockIndex(long blockIndex) => _v1?.GetClaimableBlockIndex(blockIndex) ??
                                                                       _v2?.ClaimableBlockIndex ??
                                                                       throw new InvalidOperationException();

                public long CancellableBlockIndex => _v1?.CancellableBlockIndex ??
                                                     _v2?.CancellableBlockIndex ??
                                                     throw new InvalidOperationException();

                public StakeState.StakeAchievements? Achievements => _v1?.Achievements;

                public Address Address => _v1?.address ??
                                          _v2Address ??
                                          throw new InvalidOperationException();

                public Contract? Contract => _v2?.Contract;
            }

            public StakeStateContext(StakeStateWrapper stakeStateWrapper, IAccountState accountState, long blockIndex)
                : base(accountState, blockIndex)
            {
                StakeState = stakeStateWrapper;
            }

            public StakeStateWrapper StakeState { get; }
        }

        public StakeStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                "address",
                description: "The address of current state.",
                resolve: context => context.Source.StakeState.Address);
            Field<NonNullGraphType<StringGraphType>>(
                "deposit",
                description: "The staked amount.",
                resolve: context => context.Source.AccountState.GetBalance(
                        context.Source.StakeState.Address,
                        new GoldCurrencyState((Dictionary)context.Source.GetState(GoldCurrencyState.Address)!).Currency)
                    .GetQuantityString(true));
            Field<NonNullGraphType<IntGraphType>>(
                "startedBlockIndex",
                description: "The block index the user started to stake.",
                resolve: context => context.Source.StakeState.StartedBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(
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
                resolve: context => context.Source.StakeState.GetClaimableBlockIndex(
                    context.Source.BlockIndex));
            Field<StakeAchievementsType>(
                nameof(StakeState.Achievements),
                description: "The staking achievements.",
                resolve: context => context.Source.StakeState.Achievements);
            Field<StakeRewardsType>(
                "stakeRewards",
                resolve: context =>
                {
                    if (context.Source.StakeState.Contract is not { } contract)
                    {
                        return null;
                    }

                    IReadOnlyList<IValue?> values = context.Source.GetStates(new[]
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
