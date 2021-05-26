using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ArenaRecordType : ObjectGraphType<ArenaInfo.Record>
    {
        public ArenaRecordType()
        {
            Field<IntGraphType>(
                nameof(ArenaInfo.Record.Win),
                resolve: context => context.Source.Win);
            Field<IntGraphType>(
                nameof(ArenaInfo.Record.Lose),
                resolve: context => context.Source.Lose);
            Field<IntGraphType>(
                nameof(ArenaInfo.Record.Draw),
                resolve: context => context.Source.Draw);
        }
    }
}
