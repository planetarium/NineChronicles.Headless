using GraphQL.Server;
using Libplanet.Action;

namespace NineChronicles.Headless
{
    public static class GraphQLBuilderExtensions
    {
        public static IGraphQLBuilder AddLibplanetExplorer<T>(this IGraphQLBuilder builder)
            where T : IAction, new()
        {
            builder.Services.AddLibplanetExplorer<T>();

            return builder;
        }
    }
}
