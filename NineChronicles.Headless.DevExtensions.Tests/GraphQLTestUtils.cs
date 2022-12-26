using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using TestNCAction = Libplanet.Action.PolymorphicAction<Lib9c.DevExtensions.Action.TestActionBase>;

namespace NineChronicles.Headless.DevExtensions.Tests
{
    public static class GraphQLTestUtils
    {
        public static Task<ExecutionResult> ExecuteQueryAsync<TObjectGraphType>(
            string query,
            IDictionary<string, object>? userContext = null,
            object? source = null,
            StandaloneContext? standaloneContext = null)
            where TObjectGraphType : class, IObjectGraphType
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(TObjectGraphType));
            if (!(standaloneContext is null))
            {
                services.AddSingleton(standaloneContext);
            }

            services.AddLibplanetExplorer<TestNCAction>();

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
        
        public static  TestNCAction DeserializeNCAction(IValue value)
        {
#pragma warning disable CS0612
            TestNCAction action = new TestNCAction();
#pragma warning restore CS0612
            action.LoadPlainValue(value);
            return action;
        }
    }
}
