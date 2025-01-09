using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace NineChronicles.Headless.Executable
{
    /// <summary>
    /// Periodically checks for changes to a remote configuration JSON file
    /// and applies them to the existing <see cref="IConfigurationRoot"/>.
    /// </summary>
    public class RemoteConfigReloadService : BackgroundService
    {
        private readonly IConfigurationRoot _configurationRoot;
        private readonly string _configUrl;
        private readonly TimeSpan _reloadInterval = TimeSpan.FromSeconds(30); // Check every 30 seconds
        private string? _lastConfigHash;
        private readonly ILogger _logger;

        public RemoteConfigReloadService(IConfigurationRoot configurationRoot, string configUrl)
        {
            _configurationRoot = configurationRoot;
            _configUrl = configUrl;
            _logger = Log.Logger.ForContext<RemoteConfigReloadService>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using HttpClient client = new HttpClient();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Fetch JSON
                    HttpResponseMessage response = await client.GetAsync(_configUrl, stoppingToken);
                    response.EnsureSuccessStatusCode();

                    // Compare hashes
                    string newConfigJson = await response.Content.ReadAsStringAsync(stoppingToken);
                    string newHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(newConfigJson)));
                    if (_lastConfigHash != newHash)
                    {
                        _logger.Information("[REMOTE-CONFIG-SERVICE] Remote config has changed. Updating configuration...");

                        // Build a new configuration from the fresh JSON
                        var builder = new ConfigurationBuilder();
                        using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);
                        builder.AddJsonStream(stream);
                        builder.AddEnvironmentVariables();

                        // Build a temporary config root to read the new values
                        IConfigurationRoot newConfigurationRoot = builder.Build();

                        // Overwrite existing keys
                        foreach (var kvp in newConfigurationRoot.AsEnumerable())
                        {
                            _configurationRoot[kvp.Key] = kvp.Value;
                        }

                        _lastConfigHash = newHash;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[REMOTE-CONFIG-SERVICE] Error fetching remote config: {ex.Message}");
                }

                // Wait before checking again
                await Task.Delay(_reloadInterval, stoppingToken);
            }
        }
    }
}
