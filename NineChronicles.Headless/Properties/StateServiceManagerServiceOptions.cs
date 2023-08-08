using System.ComponentModel.DataAnnotations;

namespace NineChronicles.Headless.Properties;

public class StateServiceManagerServiceOptions
{
    [Required]
    public StateService[] StateServices { get; set; } = null!;

    [Required]
    public string StateServicesDownloadPath { get; set; } = null!;

    [Required]
    public string RemoteBlockChainStatesEndpoint { get; set; } = null!;

    public class StateService
    {
        [Required]
        public string Path { get; set; } = null!;

        [Required]
        public ushort Port { get; set; }
    }
}
