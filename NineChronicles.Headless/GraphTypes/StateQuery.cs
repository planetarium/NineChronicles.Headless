using System;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;

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
                arguments: new QueryArguments(new QueryArgument<AddressType>
                {
                    Name = "address",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (!(context.Source.accountStateGetter(address) is { } state))
                    {
                        throw new InvalidOperationException($"The state {address} doesn't exists");
                    }
                    return (new AvatarState((Dictionary)state), context.Source.accountStateGetter);
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
                resolve: context =>
                {
                    if (context.Source.accountStateGetter(Addresses.Shop) is { } state)
                    {
                        return (new ShopState((Dictionary) state), context.Source.accountStateGetter);
                    }

                    return null;
                });
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

            Field<ByteStringType>(
                name: "raw",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "The address of state to fetch from the chain." }
                ),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    var state = context.Source.accountStateGetter(address);
                    return state is null ? null : new Codec().Encode(state);
                }
            );

            Field<CombinationSlotStateType>(
                name: "combinationSlot",
                description: "State for combination slot.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of combination slot."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (context.Source.accountStateGetter(address) is { } state)
                    {
                        return new CombinationSlotState((Dictionary) state);
                    }

                    return null;
                }
            );
        }
    }
}
