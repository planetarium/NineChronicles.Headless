using System;
using GraphQL.Types;
using GraphQL.Utilities;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneSchema : Schema
    {
        public StandaloneSchema(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            Query = serviceProvider.GetRequiredService<StandaloneQuery>();
            Mutation = serviceProvider.GetRequiredService<StandaloneMutation>();
            Subscription = serviceProvider.GetRequiredService<StandaloneSubscription>();
        }
    }
}
