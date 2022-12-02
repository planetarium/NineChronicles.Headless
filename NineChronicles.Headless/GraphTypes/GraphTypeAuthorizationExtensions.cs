using GraphQL;
using GraphQL.Builders;

namespace NineChronicles.Headless.GraphTypes
{
    public static class GraphTypeAuthorizationExtensions
    {
        public static FieldBuilder<TSourceType, TReturnType> AuthorizeWithLocalPolicyIf<TSourceType, TReturnType>(
            this FieldBuilder<TSourceType, TReturnType> builder,
            bool condition
        ) =>
            condition ? builder.AuthorizeWithPolicy(GraphQLService.LocalPolicyKey) : builder;
    }
}
