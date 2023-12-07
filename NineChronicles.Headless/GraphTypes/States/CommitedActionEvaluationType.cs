using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CommittedActionEvaluationType : ObjectGraphType<ICommittedActionEvaluation>
    {
        public CommittedActionEvaluationType()
        {
            Field<NonNullGraphType<BencodexValueType>>(
                name: "action",
                resolve: context => context.Source.Action
            );
            Field<NonNullGraphType<HashDigestSHA256Type>>(
                name: "inputState",
                resolve: context => context.Source.InputContext.PreviousState
            );
            Field<NonNullGraphType<HashDigestSHA256Type>>(
                name: "outputState",
                resolve: context => context.Source.OutputState
            );
            Field<StringGraphType>(
                name: "exception",
                resolve: context => context.Source.Exception?.StackTrace
            );

        }
    }
}
