using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Libplanet.Store;
using Microsoft.Extensions.DependencyInjection;
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
            IStore? store = null)
            where TObjectGraphType : class, IObjectGraphType
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(TObjectGraphType));
            if (!(standaloneContext is null))
            {
                services.AddSingleton(standaloneContext);
            }

            services.AddLibplanetExplorer<NCAction>();

            if (store is not null)
            {
                services.AddSingleton(store);   
            }

            var serviceProvider = services.BuildServiceProvider();
            return ExecuteQueryAsync<TObjectGraphType>(
                serviceProvider,
                query,
                userContext,
                source);
        }

        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            IServiceProvider serviceProvider,
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null)
            where TObjectGraphType : IObjectGraphType
        {
            var graphType = (IObjectGraphType)serviceProvider.GetService(typeof(TObjectGraphType))!;
            var documentExecutor = new DocumentExecuter();
            return documentExecutor.ExecuteAsync(new ExecutionOptions
            {
                Query = query,
                Schema = new Schema
                {
                    Query = graphType,
                },
                UserContext = userContext!,
                Root = source,
            });
        }
    }
}
