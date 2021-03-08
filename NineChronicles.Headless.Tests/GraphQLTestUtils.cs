using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Libplanet.KeyStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests
{
    public static class GraphQLTestUtils
    {
        public enum ExecutionMode
        {
            Query,
            Mutation,
            Subscription,
        }

        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            StandaloneContext? standaloneContext = null,
            ExecutionMode executionMode = ExecutionMode.Query)
            where TObjectGraphType : class, IObjectGraphType
        {
            var services = new ServiceCollection();
            return ExecuteQueryAsync<TObjectGraphType>(
                services,
                query,
                userContext,
                source,
                standaloneContext,
                executionMode);
        }
        
        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            ServiceCollection services,
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            StandaloneContext? standaloneContext = null,
            ExecutionMode executionMode = ExecutionMode.Query)
            where TObjectGraphType : class, IObjectGraphType
        {
            services.TryAddSingleton(typeof(TObjectGraphType));
            if (!(standaloneContext is null))
            {
                services.TryAddSingleton(standaloneContext);
            }

            services.TryAddSingleton(standaloneContext?.KeyStore ?? CreateRandomWeb3KeyStore());

            services.AddLibplanetExplorer<NCAction>();

            var serviceProvider = services.BuildServiceProvider();
            return ExecuteQueryAsync<TObjectGraphType>(
                serviceProvider,
                query,
                userContext,
                source,
                executionMode);
        }
        
        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            IServiceProvider serviceProvider,
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            ExecutionMode executionMode = ExecutionMode.Query)
            where TObjectGraphType : IObjectGraphType
        {
            var graphType = (IObjectGraphType)serviceProvider.GetService(typeof(TObjectGraphType));
            var documentExecutor = new DocumentExecuter();
            var schema = new Schema();

            switch (executionMode)
            {
                case ExecutionMode.Query:
                    schema.Query = graphType;
                    break;
                case ExecutionMode.Mutation:
                    schema.Mutation = graphType;
                    break;
                case ExecutionMode.Subscription:
                    schema.Subscription = graphType;
                    break;
            }

            return documentExecutor.ExecuteAsync(new ExecutionOptions
            {
                Query = query,
                Schema = schema,
                UserContext = userContext,
                Root = source,
            });
        }

        public static Web3KeyStore CreateRandomWeb3KeyStore()
        {
            return new Web3KeyStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        }
    }
}
