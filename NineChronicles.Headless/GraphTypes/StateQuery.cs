using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes
{
    public class StateQuery<T> : ObjectGraphType<BlockChain<T>>
        where T : IAction, new()
    {
        public StateQuery()
        {
            Field<AvatarStateType>(
                name: "avatar",
                arguments: new QueryArguments(new QueryArgument<AddressType>
                {
                    Name = "address",
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return new AvatarState((Dictionary)context.Source.GetState(address));
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
                    return new RankingMapState((Dictionary)context.Source.GetState(RankingState.Derive(index)));
                });
        }
    }
}
