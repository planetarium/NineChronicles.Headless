using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ChampionshipArenaStateType : ObjectGraphType<ChampionshipArenaState>
    {
        public ChampionshipArenaStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                    nameof(ChampionshipArenaState.Address),
                    resolve: context => context.Source.Address);
            Field<NonNullGraphType<IntGraphType>>(
                    nameof(ChampionshipArenaState.EndIndex),
                    resolve: context => context.Source.EndIndex);
            Field<NonNullGraphType<IntGraphType>>(
                    nameof(ChampionshipArenaState.StartIndex),
                    resolve: context => context.Source.StartIndex);
            Field<NonNullGraphType<ListGraphType<ChampionArenaInfoType>>>(
                    nameof(ChampionshipArenaState.OrderedArenaInfos),
                    resolve: context => context.Source.OrderedArenaInfos);
        }
    }
}
