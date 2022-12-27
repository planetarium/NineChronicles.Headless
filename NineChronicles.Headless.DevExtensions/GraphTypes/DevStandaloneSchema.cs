using System;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;

namespace NineChronicles.Headless.DevExtensions.GraphTypes;

public class DevStandaloneSchema : Schema
{
    public DevStandaloneSchema(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        Query = serviceProvider.GetRequiredService<DevStandaloneQuery>();
    }
}
