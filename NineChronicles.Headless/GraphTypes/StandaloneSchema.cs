using System;
using GraphQL.DI;
using GraphQL.Types;
using GraphQL.Utilities;
using Libplanet.Explorer.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneSchema : Schema
    {
        internal static readonly IConfigureSchema[] Configurations =
        {
            ConfigureLibplanetExplorerSchema.Instance,
            ConfigureNineChroniclesHeadlessSchema.Instance,
        };

        public StandaloneSchema(IServiceProvider serviceProvider)
            : base(serviceProvider, Configurations)
        {
            Query = serviceProvider.GetRequiredService<StandaloneQuery>();
            Mutation = serviceProvider.GetRequiredService<StandaloneMutation>();
            Subscription = serviceProvider.GetRequiredService<StandaloneSubscription>();
        }
    }
}
