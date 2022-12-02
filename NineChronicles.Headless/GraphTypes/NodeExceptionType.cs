using GraphQL.Types;
using Libplanet.Headless;

namespace NineChronicles.Headless.GraphTypes
{
    public sealed class NodeExceptionType : ObjectGraphType<NodeException>
    {
        public NodeExceptionType()
        {
            Field<NonNullGraphType<IntGraphType>>("code")
                .Description("The code of NodeException.")
                .Resolve(context => context.Source.Code);
            Field<NonNullGraphType<StringGraphType>>("message")
                .Description("The message of NodeException.")
                .Resolve(context => context.Source.Message);
        }
    }
}
