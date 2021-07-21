using GraphQL.Types;
using Nekoyume.Model.Stat;

namespace NineChronicles.Headless.GraphTypes.States.Models
{
    public class StatsMapType : ObjectGraphType<StatsMap>
    {
        public StatsMapType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(StatsMap.HP));
            Field<NonNullGraphType<IntGraphType>>(nameof(StatsMap.ATK));
            Field<NonNullGraphType<IntGraphType>>(nameof(StatsMap.DEF));
            Field<NonNullGraphType<IntGraphType>>(nameof(StatsMap.CRI));
            Field<NonNullGraphType<IntGraphType>>(nameof(StatsMap.HIT));
            Field<NonNullGraphType<IntGraphType>>(nameof(StatsMap.SPD));
        }
    }
}
