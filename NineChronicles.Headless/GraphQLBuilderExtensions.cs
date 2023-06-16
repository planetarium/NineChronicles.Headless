using GraphQL.Server;

namespace NineChronicles.Headless
{
    public static class GraphQLBuilderExtensions
    {
        public static IGraphQLBuilder AddLibplanetExplorer(this IGraphQLBuilder builder)
        {
            builder.Services.AddLibplanetExplorer();

            return builder;
        }
    }
}
