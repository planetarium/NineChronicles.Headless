using System;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes
{
    public class StateQuery : ObjectGraphType<(AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter)>
    {
        public StateQuery()
        {
            Name = "StateQuery";
            Field<AvatarStateType>(
                name: "avatar",
                description: "State for avatar.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (!(context.Source.accountStateGetter(address) is { } state))
                    {
                        throw new InvalidOperationException($"The state {address} doesn't exists");
                    }
                    return new AvatarState((Dictionary)state);
                });
            Field<RankingMapStateType>(
                name: "rankingMap",
                description: "State for avatar EXP record.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "RankingMapState index. 0 ~ 99"
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (context.Source.accountStateGetter(RankingState.Derive(index)) is { } state)
                    {
                        return new RankingMapState((Dictionary) state);
                    }

                    return null;
                });
            Field<ShopStateType>(
                name: "shop",
                description: "State for shop.",
                resolve: context => context.Source.accountStateGetter(Addresses.Shop) is { } state
                    ? new ShopState((Dictionary) state)
                    : null);
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                description: "State for weekly arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "WeeklyArenaState index. It increases every 56,000 blocks."
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (context.Source.accountStateGetter(WeeklyArenaState.DeriveAddress(index)) is { } state)
                    {
                        return new WeeklyArenaState((Dictionary) state);
                    }

                    return null;
                });
            Field<AgentStateType>(
                name: "agent",
                description: "State for agent.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of agent."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (context.Source.accountStateGetter(address) is { } state)
                    {
                        return (new AgentState((Dictionary) state), context.Source.accountStateGetter, context.Source.accountBalanceGetter);
                    }

                    return null;
                }
            );

            Field<StakingStateType>(
                nameof(StakingState),
                description: "State for staking.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "agentAddress",
                        Description = "Address of agent."
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "stakingRound",
                        Description = "Staking round of agent."
                    }
                ),
                resolve: context =>
                {
                    var agentAddress = context.GetArgument<Address>("agentAddress");
                    var stakingRound = context.GetArgument<int?>("stakingRound");
                    if (stakingRound is null)
                    {
                        if (context.Source.accountStateGetter(agentAddress) is { } value)
                        {
                            AgentState agentState = new AgentState((Dictionary) value);
                            stakingRound = agentState.StakingRound;
                        }
                        else
                        {
                            stakingRound = 0;
                        }
                    }
                    var stakingAddress = StakingState.DeriveAddress(agentAddress, (int) stakingRound);
                    if (context.Source.accountStateGetter(stakingAddress) is { } state)
                    {
                        return new StakingState((Dictionary) state);
                    }

                    return null;
                }
            );

            Field<StakingSheetType>(
                nameof(StakingSheet),
                resolve: context =>
                {
                    var stakingSheetAddress = Addresses.GetSheetAddress<StakingSheet>();
                    var stakingRewardSheetAddress = Addresses.GetSheetAddress<StakingRewardSheet>();
                    if (context.Source.accountStateGetter(stakingSheetAddress) is { } ss &&
                        context.Source.accountStateGetter(stakingRewardSheetAddress) is { } srs)
                    {
                        var stakingSheet = new StakingSheet();
                        stakingSheet.Set((Text) ss);
                        var stakingRewardSheet = new StakingRewardSheet();
                        stakingRewardSheet.Set((Text) srs);
                        return (stakingSheet, stakingRewardSheet);
                    }

                    return null;
                }
            );
        }
    }
}
