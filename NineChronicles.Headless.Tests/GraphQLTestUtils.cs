using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DI;
using GraphQL.Types;
using Libplanet.Explorer.Schemas;
using Microsoft.Extensions.DependencyInjection;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests
{
    public static class GraphQLTestUtils
    {
        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            StandaloneContext? standaloneContext = null,
            bool allowErrors = false)
            where TObjectGraphType : class, IObjectGraphType
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(TObjectGraphType));
            if (!(standaloneContext is null))
            {
                services.AddSingleton(standaloneContext);
            }

            services.AddLibplanetExplorer<NCAction>();

            var serviceProvider = services.BuildServiceProvider();
            return ExecuteQueryAsync<TObjectGraphType>(
                serviceProvider,
                query,
                userContext,
                source,
                allowErrors);
        }

        public static async Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            IServiceProvider serviceProvider,
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            bool allowErrors = false)
            where TObjectGraphType : IObjectGraphType
        {
            var graphType = (IObjectGraphType)serviceProvider.GetService(typeof(TObjectGraphType))!;
            var documentExecutor = new DocumentExecuter();
            var result = await documentExecutor.ExecuteAsync(new ExecutionOptions
            {
                Query = query,
                Schema = new Schema(new DefaultServiceProvider(), StandaloneSchema.Configurations)
                {
                    Query = graphType,
                },
                UserContext = userContext!,
                Root = source,
            });
            if (!allowErrors && result.Errors is { } errors)
            {
                Assert.True(
                    false,
                    $"The query failed with the following errors:\n\t{string.Join("\n\t", errors)}\n\nQuery:\n{query}"
                );
            }

            return result;
        }
    }
}
