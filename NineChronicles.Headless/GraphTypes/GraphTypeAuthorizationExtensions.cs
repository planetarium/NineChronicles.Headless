using GraphQL;
using GraphQL.Server.Authorization.AspNetCore;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public static class GraphTypeAuthorizationExtensions
    {
        public static FieldType AuthorizeWithLocalPolicyIf(this FieldType fieldType, bool condition) =>
            condition ? fieldType.AuthorizeWith(GraphQLService.LocalPolicyKey) : fieldType;
    }
}
