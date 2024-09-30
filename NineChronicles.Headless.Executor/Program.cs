using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cocona;
using Cocona.Docs;
using Libplanet.Common;
using Libplanet.Crypto;
using NineChronicles.Headless.Executor.Client;
using NineChronicles.Headless.Executor.Constants;
using NineChronicles.Headless.Executor.Models;

namespace NineChronicles.Headless.Executor;

[HasSubCommands(typeof(DocumentCommand), "docs")]
public class Program
{
    static async Task Main(string[] args)
    {
        await CoconaLiteApp.RunAsync<Program>(args);
    }

    [Command(Description = "Install a headless")]
    public async Task Install(
        [Argument(Description = "The version of NineChronicles.Headless to download")]
            string version,
        [Option(
            Description = "The OS platform to download for (optional, will auto-detect if not provided)"
        )]
            string? os = null
    )
    {
        var client = new GithubClient();
        Console.WriteLine($"Downloading version {version} of NineChronicles Headless...");
        await client.DownloadAndExtract(version, os);
        Console.WriteLine("Installation complete!");
    }

    [Command(Description = "List installed headless versions")]
    public async Task Versions(
        [Option(Description = "List remote versions available for download")] bool remote = false,
        [Option(Description = "Page number for remote version pagination")] int page = 1
    )
    {
        if (remote)
        {
            var client = new GithubClient();
            await client.ListRemoteVersions(page);
        }
        else
        {
            var planetManager = new PlanetManager();
            planetManager.ListLocalVersions();
        }
    }

    [Command(Description = "Run Headless")]
    public async Task Run(string version, Planet planet)
    {
        string extractedPath = Path.Combine(Paths.HeadlessPath, version);
        string headlessDllPath = Path.Combine(
            extractedPath,
            "NineChronicles.Headless.Executable.dll"
        );
        if (!File.Exists(headlessDllPath))
        {
            throw new FileNotFoundException($"Headless DLL not found in path {headlessDllPath}");
        }

        var appSettingsData = new AppSettingsData();
        string storePath = Path.Combine(Paths.StorePath, version, planet.ToString());
        var settingsClient = new SettingsClient();

        switch (planet)
        {
            case Planet.MainnetOdin:
            case Planet.MainnetHeimdall:
                var headlessClient = new HeadlessClient();
                var apv = await headlessClient.GetApvAsync(
                    PlanetEndpoints.GetGraphQLEndpoint(planet)
                );

                appSettingsData = new AppSettingsData
                {
                    Apv = apv.ToToken(),
                    StorePath = storePath,
                };

                if (!Directory.Exists(storePath))
                {
                    Console.WriteLine($"Warning: Store path does not exist: {storePath}");
                    Console.WriteLine(
                        $"It is recommended to download and extract the snapshot to {storePath} to avoid starting synchronization from block 0, which can take a long time."
                    );
                }
                await CreateAppSettingsFile(planet, settingsClient, appSettingsData);
                break;
            case Planet.Single:
                var pkey = new PrivateKey(Secret.PrivateKeyForSingleNode);
                string genesisBlockPath = Path.Combine(
                    Paths.GenesisBlockPath,
                    "genesis-block-for-single"
                );

                if (!File.Exists(genesisBlockPath))
                {
                    Directory.CreateDirectory(Paths.GenesisBlockPath);
                    await settingsClient.DownloadGenesisBlockAsync(genesisBlockPath);
                }

                appSettingsData = new AppSettingsData
                {
                    Apv = "1/b4179Ad0d7565A6EcFA70d2a0f727461039e0159/MEUCIQDvIIp8IKCpjKojE8LzgYZzeRg9fUPl.sWHrowzHhmrxgIgBhTkSRc8BHXZwwIAwBQN8J3wGlAbOD7FRyp8bA6OH6Y=",
                    StorePath = storePath,
                    GenesisBlockPath = genesisBlockPath,
                    MinerPrivateKeyString = ByteUtil.Hex(pkey.ToByteArray()),
                    ConsensusPrivateKeyString = ByteUtil.Hex(pkey.ToByteArray()),
                    ConsensusSeedPublicKey = ByteUtil.Hex(pkey.PublicKey.Format(true))
                };
                await CreateAppSettingsFile(planet, settingsClient, appSettingsData);
                break;
        }

        var appsettingsPath = Path.Combine(Paths.AppsettingsPath, $"appsettings.{planet}.json");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{headlessDllPath} --config {appsettingsPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = extractedPath
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Console.WriteLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Console.Error.WriteLine(args.Data);
                }
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
        }
    }

    private async Task CreateAppSettingsFile(
        Planet planet,
        SettingsClient client,
        AppSettingsData data
    )
    {
        string templatePath = Path.Combine(Paths.AppsettingsTplPath, $"appsettings.{planet}.tpl");

        if (!File.Exists(templatePath))
        {
            Directory.CreateDirectory(Paths.AppsettingsTplPath);
            await client.DownloadTemplateAsync(planet, templatePath);
        }

        var appsettingsPath = Path.Combine(Paths.AppsettingsPath, $"appsettings.{planet}.json");
        string result;

        if (!File.Exists(appsettingsPath))
        {
            var templateContent = File.ReadAllText(templatePath);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                templateContent = templateContent
                    .Replace("${Apv}", data.Apv)
                    .Replace("${GenesisBlockPath}", data.GenesisBlockPath.Replace("\\", "\\\\"))
                    .Replace("${StorePath}", data.StorePath.Replace("\\", "\\\\"))
                    .Replace("${MinerPrivateKeyString}", data.MinerPrivateKeyString)
                    .Replace("${ConsensusPrivateKeyString}", data.ConsensusPrivateKeyString)
                    .Replace("${ConsensusSeedPublicKey}", data.ConsensusSeedPublicKey);
            }
            else
            {
                templateContent = templateContent
                    .Replace("${Apv}", data.Apv)
                    .Replace("${GenesisBlockPath}", data.GenesisBlockPath)
                    .Replace("${StorePath}", data.StorePath)
                    .Replace("${MinerPrivateKeyString}", data.MinerPrivateKeyString)
                    .Replace("${ConsensusPrivateKeyString}", data.ConsensusPrivateKeyString)
                    .Replace("${ConsensusSeedPublicKey}", data.ConsensusSeedPublicKey);
            }
            result = templateContent;
        }
        else
        {
            var appsettingsContent = File.ReadAllText(appsettingsPath);
            var jsonDoc = JsonNode.Parse(appsettingsContent);
            if (jsonDoc != null && jsonDoc["Headless"] != null)
            {
                jsonDoc["Headless"]["AppProtocolVersionString"] = data.Apv;
            }
            result = jsonDoc.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        Directory.CreateDirectory(Paths.AppsettingsPath);
        File.WriteAllText(appsettingsPath, result);
    }
}
