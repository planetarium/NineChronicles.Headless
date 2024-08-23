using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;
using Libplanet.Crypto;
using Nekoyume.Blockchain;
using Serilog;

namespace NineChronicles.Headless.Services
{
    public class RedisAccessControlService : IAccessControlService
    {
        private static readonly Dictionary<string, string> Whitelist = new Dictionary<string, string>
        {
            { "0x3fe3106a3547488e157AED606587580e80375295", "100" },
            { "0x4654C573FC9608eb47869224DD879d3E9571108E", "100" },
            { "0xCbfC996ad185c61a031f40CeeE80a055e6D83005", "4" },
            { "0x5Cca7979be74514E69bfD754315a923Ca9575249", "1" },
            { "0xec48c68198dA91e89d6CA4eff93C23441e167358", "100" },
            { "0xCb75C84D76A6f97A2d55882Aea4436674c288673", "100" },
            { "0x0181300F1e04603c3e55c42F5e95d1c70D26ef0A", "100" },
            { "0xDB7a028A4BbaA2BBB5f8Dce78e6a642E755eC1bb", "1" },
            { "0x3743EEa8bbdB8b19261d50Ceb7Efacc9264C5bbD", "1" },
            { "0xc1Ea0D238f7b1C9c70Ce6589DAb2F15C1A076455", "100" },
            { "0x379781Af2011e482A754A12b3A32f2Ed4Ffd89e3", "1" },
            { "0xCFCd6565287314FF70e4C4CF309dB701C43eA5bD", "50" },
            { "0x349b75BfA9c1440E5dBC52eA6aEa32Fa39fA1c33", "1" },
            { "0x0E19A992ad976B4986098813DfCd24B0775AC0AA", "100" },
            { "0xCaD60f18b4Ba189f7f1c14E2267D9b20F5b16Ff5", "100" },
            { "0x4c35e816c9e13628615581a436a0df38F57A08cc", "100" },
            { "0xe19e13dC613F95b5F641CE2b25353396DcBFE775", "100" },
            { "0xb370948765159F6064f2E8EfC0eB1933CD6f2922", "100" },
            { "0x5D383B8842F8313cF99b812e435C9A6af9e6a8CD", "100" },
            { "0x9093dd96c4bb6b44A9E0A522e2DE49641F146223", "100" },
            { "0x4654c573fc9608eb47869224dd879d3e9571108e", "1" },
            { "0x1c2ae97380CFB4F732049e454F6D9A25D4967c6f", "100" },
            { "0x03661edeE0E318251aDD886F20471814e5C607E3", "0" },
            { "0xB75e727f890b42FBE202AD33b836ec5Ea2eeb58d", "0" },
            { "0x491d9842ED8F1b5D291272CF9e7B66a7B7C90cda", "100" },
            { "0xc64c7cBf29BF062acC26024D5b9D1648E8f8D2e1", "100" },
            { "0x590c887BDac8d957Ca5d3c1770489Cf2aFBd868E", "1" }
        };

        protected IDatabase _db;

        public RedisAccessControlService(string storageUri)
        {
            var configurationOptions = new ConfigurationOptions
            {
                EndPoints = { storageUri },
                ConnectTimeout = 500,
                SyncTimeout = 500,
            };

            var redis = ConnectionMultiplexer.Connect(configurationOptions);
            _db = redis.GetDatabase();
        }

        public async Task<int?> GetTxQuotaAsync(Address address)
        {
            if (Whitelist.TryGetValue(address.ToString(), out var result))
            {
                return await Task.FromResult<int?>(Convert.ToInt32(result));
            }
            
            return await Task.FromResult<int?>(null);
        }
    }
}
