using System;
using GraphQL.Types;
using GraphQL.Utilities;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneSchema : Schema
    {
        public StandaloneSchema(IServiceProvider serviceProvider)
        {
            Query = serviceProvider.GetRequiredService<StandaloneQuery>();
            Mutation = serviceProvider.GetRequiredService<StandaloneMutation>();
            Subscription = serviceProvider.GetRequiredService<StandaloneSubscription>();
            Services = serviceProvider;
        }
    }
}
