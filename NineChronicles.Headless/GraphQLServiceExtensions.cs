using System;
using System.Linq;
using System.Reflection;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NineChronicles.Headless
{
    public static class GraphQLServiceExtensions
    {
        public static IServiceCollection AddGraphTypes(this IServiceCollection services)
        {
            var graphTypes = Assembly.GetAssembly(typeof(GraphQLServiceExtensions))?.GetTypes().Where(
                type => type.Namespace is {} @namespace &&
                        @namespace.StartsWith($"{nameof(NineChronicles)}.{nameof(Headless)}.{nameof(GraphTypes)}") &&
                        (typeof(IGraphType).IsAssignableFrom(type) || typeof(ISchema).IsAssignableFrom(type)) &&
                        !type.IsAbstract);

            foreach (Type graphType in graphTypes)
            {
                services.TryAddSingleton(graphType);
            }

            return services;
        }
    }
}
