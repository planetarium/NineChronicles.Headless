using NineChronicles.Headless.Executor.Constants;

namespace NineChronicles.Headless.Executor.Client;

public class SettingsClient
{
    private readonly HttpClient _httpClient;

    public SettingsClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(
                "http://settings.planetariumhq.com.s3-website.us-east-2.amazonaws.com/"
            )
        };
    }

    public async Task DownloadTemplateAsync(Planet planet, string destinationPath)
    {
        string relativeUrl = $"templates/appsettings.{planet}.tpl";
        await DownloadFileAsync(relativeUrl, destinationPath);
    }

    public async Task DownloadGenesisBlockAsync(string destinationPath)
    {
        string relativeUrl = "genesis/genesis-block-for-single";
        await DownloadFileAsync(relativeUrl, destinationPath);
    }

    private async Task DownloadFileAsync(string relativeUrl, string destinationPath)
    {
        var response = await _httpClient.GetAsync(relativeUrl);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );
        await response.Content.CopyToAsync(fs);
    }
}
