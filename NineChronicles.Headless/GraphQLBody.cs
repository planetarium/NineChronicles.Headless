using Newtonsoft.Json.Linq;

namespace NineChronicles.Headless
{
    public class GraphQLBody
    {
        public string Query { get; set; }

        public JObject Variables { get; set; }
    }
}
