using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    internal class ArenaSimulationResultType : ObjectGraphType<ArenaSimulationResult>
    {
        public ArenaSimulationResultType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ArenaSimulationResult.seed),
                resolve: context => context.Source.seed);
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(ArenaSimulationResult.win),
                resolve: context => context.Source.win);
        }
    }
}
