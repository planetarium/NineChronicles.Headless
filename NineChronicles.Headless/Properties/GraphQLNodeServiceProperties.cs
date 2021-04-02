namespace NineChronicles.Headless.Properties
{
    public class GraphQLNodeServiceProperties
    {
        public bool GraphQLServer { get; set; }

        public string? GraphQLListenHost { get; set; }

        public int? GraphQLListenPort { get; set; }
        
        public string? AdminPassphrase { get; set; }

        public bool NoCors { get; set; }
    }
}
