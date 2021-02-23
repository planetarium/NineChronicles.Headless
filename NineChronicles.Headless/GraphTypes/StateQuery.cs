using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
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
                    Description = "Address of AvatarState."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return new AvatarState((Dictionary)context.Source(address));
                });
            Field<RankingMapStateType>(
                name: "rankingMap",
                description: "State for Record AvatarState EXP.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "RankingMapState index. 0 ~ 99"
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    return new RankingMapState((Dictionary)context.Source(RankingState.Derive(index)));
                });
            Field<ShopStateType>(
                name: "shop",
                description: "State for market.",
                resolve: context => new ShopState((Dictionary) context.Source(Addresses.Shop)));
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                description: "State for arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "WeeklyArenaState index. It increases every 56,000 blocks."
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    return new WeeklyArenaState(
                        (Dictionary) context.Source(WeeklyArenaState.DeriveAddress(index)));
                });
            Field<AgentStateType>(
                name: "agent",
                description: "State for account.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of AgentState."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return new AgentState((Dictionary) context.Source(address));
                }
            );
        }
    }
}
