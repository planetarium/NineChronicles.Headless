using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class RankingMapStateType : ObjectGraphType<RankingMapState>
    {
        public RankingMapStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingMapState.address),
                resolve: context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingMapState.Capacity),
                resolve: context => RankingMapState.Capacity);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<RankingInfoType>>>>(
                "rankingInfos",
                resolve: context => context.Source.GetRankingInfos(null));
        }
    }
}
