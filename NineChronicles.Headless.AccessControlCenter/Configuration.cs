namespace NineChronicles.Headless.AccessControlCenter
{
    public class Configuration
    {
        public int Port { get; set; }

        public string AccessControlServiceType { get; set; } = null!;

        public string AccessControlServiceConnectionString { get; set; } = null!;
    }
}
