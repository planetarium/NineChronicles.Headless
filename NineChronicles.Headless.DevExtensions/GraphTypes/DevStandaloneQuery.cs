using GraphQL.Types;
using Microsoft.Extensions.Configuration;

namespace NineChronicles.Headless.DevExtensions.GraphTypes;

public class DevStandaloneQuery : ObjectGraphType
{
    public DevStandaloneQuery(StandaloneContext standaloneContext, IConfiguration configuration,
        ActionEvaluationPublisher publisher)
    {
        Field<NonNullGraphType<DevActionQuery>>(
            name: "devActionQuery",
            resolve: context => new DevActionQuery(standaloneContext));
    }
}
