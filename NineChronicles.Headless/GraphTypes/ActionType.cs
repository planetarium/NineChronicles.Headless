using GraphQL.Types;
using Libplanet.Action;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionType<T> : ObjectGraphType<T> where T : IAction, new()
    {
        public ActionType()
        {
            Field<NonNullGraphType<StringGraphType>>(
                nameof(IAction.PlainValue),
                resolve: context => context.Source.PlainValue.Inspection
            );
            Name = "Action";
        }
    }
}
