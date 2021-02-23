using GraphQL.Types;
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
                description: "Address of RankingMapState.",
                resolve: context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingMapState.Capacity),
                description: "RankingMapState Capacity.",
                resolve: context => RankingMapState.Capacity);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<RankingInfoType>>>>(
                "rankingInfos",
                description: "List of RankingInfo.",
                resolve: context => context.Source.GetRankingInfos(null));
        }
    }
}
