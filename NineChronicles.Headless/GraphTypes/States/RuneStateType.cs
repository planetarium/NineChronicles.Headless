using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class RuneStateType : ObjectGraphType<RuneState>
    {
        public RuneStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(RuneState.RuneId));
            Field<NonNullGraphType<IntGraphType>>(nameof(RuneState.Level));
        }
    }
}
