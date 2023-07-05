using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ArenaSimulationStateType : ObjectGraphType<ArenaSimulationState>
    {
        public ArenaSimulationStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ArenaSimulationState.blockIndex),
                description: "Block Index",
                resolve: context => context.Source.blockIndex);
            Field<NonNullGraphType<ListGraphType<ArenaSimulationResultType>>>(
                nameof(ArenaSimulationState.result),
                description: "Simulation Result",
                resolve: context => context.Source.result);
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(ArenaSimulationState.winPercentage),
                description: "Win percentage",
                resolve: context => context.Source.winPercentage);
        }
    }
}
