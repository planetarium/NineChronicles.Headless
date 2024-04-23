using System;
using System.Threading.Tasks;
using System.Net.Http;
using Libplanet.Crypto;
using Nekoyume.Blockchain;
using Serilog;

namespace NineChronicles.Headless.Services
{
    public class RestAPIAccessControlService : IAccessControlService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public RestAPIAccessControlService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(500)
            };
        }

        public Task<int?> GetTxQuotaAsync(Address address)
        {
            try
            {
                string requestUri = $"{_baseUrl}/entries/{address}";
                HttpResponseMessage response = _httpClient.GetAsync(requestUri).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string resultString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return Task.FromResult<int?>(Convert.ToInt32(resultString));
                }
            }
            catch (TaskCanceledException)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Error("RestAPI timeout for \"{Address}\".", address);
            }
            catch (HttpRequestException ex)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Error(ex, "HttpRequestException occurred for \"{Address}\".", address);
            }

            return Task.FromResult<int?>(null);
        }
    }
}
