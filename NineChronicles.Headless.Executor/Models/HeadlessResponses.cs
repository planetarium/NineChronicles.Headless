using System.Text;
using System.Text.Json.Serialization;

namespace NineChronicles.Headless.Executor.Models;

public class GraphQLRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; }
}

public class AppProtocolVersion
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("signer")]
    public string Signer { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonPropertyName("extra")]
    public string Extra { get; set; }

    public string ToToken()
    {
        byte[] signatureBytes = Convert.FromHexString(Signature);
        string sig = Convert.ToBase64String(signatureBytes).Replace('/', '.');

        var prefix = $"{Version}/{Signer.Remove(0, 2)}/{sig}";

        if (Extra is not null)
        {
            byte[] extraBytes = Convert.FromHexString(Extra);
            string extra = Convert.ToBase64String(extraBytes).Replace('/', '.');
            return $"{prefix}/{extra}";
        }

        return prefix;
    }
}

public class NodeStatus
{
    [JsonPropertyName("appProtocolVersion")]
    public AppProtocolVersion AppProtocolVersion { get; set; }
}

public class Data
{
    [JsonPropertyName("nodeStatus")]
    public NodeStatus NodeStatus { get; set; }
}

public class GraphQLResponse
{
    [JsonPropertyName("data")]
    public Data Data { get; set; }
}
