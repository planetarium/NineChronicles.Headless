using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NineChronicles.Headless.AccessControlCenter
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Get configuration
            string configPath =
                Environment.GetEnvironmentVariable("ACS_CONFIG_FILE") ?? "appsettings.json";

            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(configPath)
                .AddEnvironmentVariables("ACS_");
            IConfiguration config = configurationBuilder.Build();

            var acsConfig = new Configuration();
            config.Bind(acsConfig);

            var service = new AcsService(
                acsConfig.AccessControlServiceType,
                acsConfig.AccessControlServiceConnectionString
            );
            var hostBuilder = service.Configure(Host.CreateDefaultBuilder(), acsConfig.Port);
            var host = hostBuilder.Build();
            host.Run();
        }
    }
}
