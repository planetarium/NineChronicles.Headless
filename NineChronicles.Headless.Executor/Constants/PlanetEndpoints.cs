namespace NineChronicles.Headless.Executor.Constants;

public static class PlanetEndpoints
{
    public static readonly IReadOnlyDictionary<Planet, string> GraphQLEndpoints = new Dictionary<
        Planet,
        string
    >
    {
        { Planet.MainnetOdin, "https://9c-main-rpc-1.nine-chronicles.com/graphql" },
        { Planet.MainnetHeimdall, "https://heimdall-rpc-1.nine-chronicles.com/graphql" }
    };

    public static string GetGraphQLEndpoint(Planet planet)
    {
        if (GraphQLEndpoints.TryGetValue(planet, out string endpoint))
        {
            return endpoint;
        }
        throw new Exception(
            $"GraphQL endpoint not defined for planet: {planet}. Please ensure the planet is correctly mapped in {nameof(GraphQLEndpoints)}."
        );
    }
}
