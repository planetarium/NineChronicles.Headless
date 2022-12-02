using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ArenaRecordType : ObjectGraphType<ArenaInfo.Record>
    {
        public ArenaRecordType()
        {
            Field<IntGraphType>(nameof(ArenaInfo.Record.Win))
                .Resolve(context => context.Source.Win);
            Field<IntGraphType>(nameof(ArenaInfo.Record.Lose))
                .Resolve(context => context.Source.Lose);
            Field<IntGraphType>(nameof(ArenaInfo.Record.Draw))
                .Resolve(context => context.Source.Draw);
        }
    }
}
