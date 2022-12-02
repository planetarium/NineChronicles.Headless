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
            Field<NonNullGraphType<AddressType>>(nameof(RankingMapState.address))
                .Description("Address of RankingMapState.")
                .Resolve(context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(nameof(RankingMapState.Capacity))
                .Description("RankingMapState Capacity.")
                .Resolve(context => RankingMapState.Capacity);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<RankingInfoType>>>>(
                "rankingInfos")
                .Description("List of RankingInfo.")
                .Resolve(context => context.Source.GetRankingInfos(null));
        }
    }
}
