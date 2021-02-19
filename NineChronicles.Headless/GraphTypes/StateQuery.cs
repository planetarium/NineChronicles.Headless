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
                arguments: new QueryArguments(new QueryArgument<AddressType>
                {
                    Name = "address",
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return new AvatarState((Dictionary)context.Source(address));
                });
            Field<RankingMapStateType>(
                name: "rankingMap",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    return new RankingMapState((Dictionary)context.Source(RankingState.Derive(index)));
                });
            Field<ShopStateType>(
                name: "shop",
                resolve: context => new ShopState((Dictionary) context.Source(Addresses.Shop)));
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    return new WeeklyArenaState(
                        (Dictionary) context.Source(WeeklyArenaState.DeriveAddress(index)));
                });
            Field<AgentStateType>(
                name: "agent",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
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
