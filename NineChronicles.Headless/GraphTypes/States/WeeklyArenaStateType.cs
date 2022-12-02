using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class WeeklyArenaStateType : ObjectGraphType<WeeklyArenaState>
    {
        public WeeklyArenaStateType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(WeeklyArenaState.address))
                .Resolve(context => context.Source.address);
            Field<NonNullGraphType<BooleanGraphType>>(nameof(WeeklyArenaState.Ended))
                .Resolve(context => context.Source.Ended);
            Field<NonNullGraphType<ListGraphType<ArenaInfoType>>>(
                nameof(WeeklyArenaState.OrderedArenaInfos))
                .Resolve(context => context.Source.OrderedArenaInfos);
        }
    }
}
