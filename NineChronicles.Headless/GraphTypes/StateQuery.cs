using System;
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
    public class StateQuery : ObjectGraphType<AccountStateGetter>
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
                    if (!(context.Source(address) is { } state))
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
                    if (context.Source(RankingState.Derive(index)) is { } state)
                    {
                        return new RankingMapState((Dictionary) state);
                    }

                    return null;
                });
            Field<ShopStateType>(
                name: "shop",
                description: "State for shop.",
                resolve: context => context.Source(Addresses.Shop) is { } state
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
                    if (context.Source(WeeklyArenaState.DeriveAddress(index)) is { } state)
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
                    if (context.Source(address) is { } state)
                    {
                        return new AgentState((Dictionary) state);
                    }

                    return null;
                }
            );
        }
    }
}
