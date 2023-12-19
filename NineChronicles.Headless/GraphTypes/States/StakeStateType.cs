using System;
using System.Collections.Generic;
using System.Linq;
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
using Nekoyume.Action;
using Nekoyume.Extensions;
using Nekoyume.Model.Item;
using Nekoyume.Model.Stake;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.Abstractions;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StakeStateType : ObjectGraphType<StakeStateType.StakeStateContext>
    {
        public class StakeStateContext : StateContext
        {
            public StakeStateContext(StakeStateV2 stakeState, Address address, Address agentAddress, IAccountState accountState, long blockIndex, StateMemoryCache stateMemoryCache)
                : base(accountState, blockIndex, stateMemoryCache)
            {
                StakeState = stakeState;
                Address = address;
                AgentAddress = agentAddress;
            }

            public StakeStateV2 StakeState { get; }
            public Address Address { get; }
            
            public Address AgentAddress { get; }
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
                resolve: context => context.Source.AccountState.GetBalance(
                        context.Source.Address,
                        new GoldCurrencyState((Dictionary)context.Source.GetState(GoldCurrencyState.Address)!).Currency)
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
                nameof(StakeState.Achievements),
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
            Field<NonNullGraphType<StakeRewardsType2>>(
                "stakeRewards2",
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

                    var accountState = context.Source.AccountState;
                    var ncg = accountState.GetGoldCurrency();
                    var stakedNcg = accountState.GetBalance(context.Source.Address, ncg);
                    var stakingLevel = Math.Min(
                        stakeRegularRewardSheet.FindLevelByStakedAmount(
                            context.Source.AgentAddress,
                            stakedNcg),
                        stakeRegularRewardSheet.Keys.Max());
                    var itemSheet = accountState.GetItemSheet();
                    accountState.TryGetStakeStateV2(context.Source.AgentAddress, out var stakeStateV2);
                    // The first reward is given at the claimable block index.
                    var rewardSteps = stakeStateV2.ClaimableBlockIndex == context.Source.BlockIndex
                        ? 1
                        : 1 + (int)Math.DivRem(
                            context.Source.BlockIndex - stakeStateV2.ClaimableBlockIndex,
                            stakeStateV2.Contract.RewardInterval,
                            out _);

                    var random = new Random();
                    var result = StakeRewardCalculator.CalculateFixedRewards(stakingLevel, random, stakeRegularFixedRewardSheet,
                        itemSheet, rewardSteps);
                    var (itemResult, favResult) = StakeRewardCalculator.CalculateRewards(ncg, stakedNcg, stakingLevel, rewardSteps,
                        stakeRegularRewardSheet, itemSheet, random);
                    result = (Dictionary<ItemBase, int>) result.Union(itemResult);
                    return (result, favResult);
                }
            );
        }

        public class Random : IRandom
        {
            private readonly System.Random _random;

            public Random(int seed = default)
            {
                _random = new System.Random(seed);
            }

            public int Seed => 0;

            public int Next()
            {
                return _random.Next();
            }

            public int Next(int maxValue)
            {
                return _random.Next(maxValue);
            }

            public int Next(int minValue, int maxValue)
            {
                return _random.Next(minValue, maxValue);
            }

            public void NextBytes(byte[] buffer)
            {
                _random.NextBytes(buffer);
            }

            public double NextDouble()
            {
                return _random.NextDouble();
            }
        }
    }
}
