using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Libplanet.Crypto;
using Nekoyume.Blockchain;
using Serilog;

namespace NineChronicles.Headless.Services;

public class LocalAccessControlService : IAccessControlService
{
    private Dictionary<string, string> _whitelist;
    public LocalAccessControlService(string connectionString)
    {
        try
        {
            _whitelist = Task.Run(() => LoadDataAsync(connectionString)).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log.Error(e, "Invalid AccessControlService Whitelist");
            throw;
        }
    }

    private async Task<Dictionary<string, string>> LoadDataAsync(string url)
    {
        using HttpClient client = new HttpClient();
        string jsonString = await client.GetStringAsync(url);

        if (string.IsNullOrEmpty(jsonString))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString)
               ?? new Dictionary<string, string>();
    }

    public async Task<int?> GetTxQuotaAsync(Address address)
    {
        if (_whitelist.TryGetValue(address.ToString(), out var result))
        {
            return await Task.FromResult<int?>(Convert.ToInt32(result));
        }

        return await Task.FromResult<int?>(null);
    }
}
