using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using NineChronicles.Headless.Executor.Constants;

namespace NineChronicles.Headless.Executor.Client;

public class GithubClient
{
    private static readonly HttpClient _client = new HttpClient();
    private static readonly string BaseDownloadUrl =
        "https://github.com/planetarium/NineChronicles.Headless/releases/download/";
    private static readonly string TempDirectory = Path.Combine(
        Path.GetTempPath(),
        "NineChroniclesHeadless"
    );
    private static readonly string GitHubReleasesApiUrl =
        "https://api.github.com/repos/planetarium/NineChronicles.Headless/releases";

    public async Task<string> DownloadAndExtract(string version, string? os = null)
    {
        os ??= GetOS();
        string url = BuildDownloadUrl(version, os);
        string downloadPath = await DownloadFile(url, version, os);
        string extractPath = ExtractFile(downloadPath, version);

        DeleteCompressedFile(downloadPath);

        return extractPath;
    }

    public async Task ListRemoteVersions(int page = 1)
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NineChronicles.Headless");

        string url = $"{GitHubReleasesApiUrl}?page={page}&per_page=10";

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(content);

        if (releases == null || releases.Count == 0)
        {
            Console.WriteLine("No remote versions found.");
            return;
        }

        Console.WriteLine($"Remote versions (Page {page}):");
        foreach (var release in releases)
        {
            Console.WriteLine(release.tag_name);
        }
    }

    private class GitHubRelease
    {
        public string tag_name { get; set; } = string.Empty;
    }

    private string GetOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        }
        throw new NotSupportedException("Unsupported OS platform.");
    }

    private string BuildDownloadUrl(string version, string os)
    {
        string fileExtension = GetFileExtension(os);
        return $"{BaseDownloadUrl}{version}/NineChronicles.Headless-{os}.{fileExtension}";
    }

    private string GetFileExtension(string os)
    {
        return os.StartsWith("win") ? "zip" : "tar.xz";
    }

    private async Task<string> DownloadFile(string url, string version, string os)
    {
        string fileName = Path.Combine(
            TempDirectory,
            $"NineChronicles.Headless-{version}-{os}.{GetFileExtension(os)}"
        );
        Directory.CreateDirectory(TempDirectory);

        using (var response = await _client.GetAsync(url))
        {
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(fileName, FileMode.Create);
            await response.Content.CopyToAsync(fs);
        }

        return fileName;
    }

    private string ExtractFile(string filePath, string version)
    {
        string extractFolder = GetExtractionPath(version);
        Directory.CreateDirectory(extractFolder);

        if (filePath.EndsWith(".zip"))
        {
            ZipFile.ExtractToDirectory(filePath, extractFolder);
        }
        else if (filePath.EndsWith(".tar.xz"))
        {
            ExtractTarXZ(filePath, extractFolder);
        }

        return extractFolder;
    }

    private string GetExtractionPath(string version)
    {
        return Path.Combine(Paths.HeadlessPath, version);
    }

    private void ExtractTarXZ(string filePath, string extractFolder)
    {
        string command = $"tar -xf {filePath} -C {extractFolder}";
        System.Diagnostics.Process.Start("/bin/bash", $"-c \"{command}\"").WaitForExit();
    }

    private void DeleteCompressedFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
