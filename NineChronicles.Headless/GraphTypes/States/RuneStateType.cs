using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States;

public class RuneStateType : ObjectGraphType<RuneState>
{
    public RuneStateType()
    {
        Field<NonNullGraphType<IntGraphType>>(
            nameof(RuneState.RuneId),
            description: "ID of rune.",
            resolve: context => context.Source.RuneId
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(RuneState.Level),
            description: "Level of this rune.",
            resolve: context => context.Source.Level
        );
    }
}
