using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NineChronicles.Headless.Executor.Models;

namespace NineChronicles.Headless.Executor.Client;

public class HeadlessClient
{
    private readonly HttpClient _client = new HttpClient();
    public const string GetApvQuery =
        @"{
            nodeStatus {
            appProtocolVersion {
                version
                signer
                signature
                extra
            }
            }
        }";

    public async Task<AppProtocolVersion> GetApvAsync(string headlessUrl)
    {
        var request = new GraphQLRequest { Query = GetApvQuery };
        var jsonRequest = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, headlessUrl)
        {
            Content = content
        };

        var response = await _client.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();

        var graphQLResponse = JsonSerializer.Deserialize<GraphQLResponse>(jsonResponse);

        if (graphQLResponse == null)
        {
            throw new Exception("Failed to deserialize GraphQL response");
        }

        return graphQLResponse.Data.NodeStatus.AppProtocolVersion;
    }
}
