using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Nekoyume.Action;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Tests.IO;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class GenesisCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly GenesisCommand _command;
        private readonly PrivateKey _privateKey;
        private readonly BlockHash _blockHash;

        public GenesisCommandTest()
        {
            _console = new StringIOConsole();
            _command = new GenesisCommand(_console);
            _privateKey = new PrivateKey();
            _blockHash = BlockHash.FromHashDigest(default);
        }

        [Theory]
        [InlineData(true, true, false)]
        [InlineData(true, false, false)]
        [InlineData(false, true, true)]
        [InlineData(false, false, true)]
        public void Mine(bool tablePathExist, bool goldDistributionExist, bool exc)
        {
            var config = new Dictionary<string, object>();

            // DataConfig: tablePath
            var tablePath = tablePathExist
                ? Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Lib9c", "Lib9c", "TableCSV"))
                : "";

            // Avoid JsonException in Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (tablePathExist)
                {
                    tablePath = tablePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }

            config["data"] = new Dictionary<string, object>
            {
                ["tablePath"] = tablePath,
            };

            // CurrencyConfig: initialMinter, initialCurrencyDeposit
            List<GoldDistribution>? goldDistribution = null;
            if (goldDistributionExist)
            {
                goldDistribution = new List<GoldDistribution>
                {
                    new GoldDistribution
                    {
                        Address = new Address("F9A15F870701268Bd7bBeA6502eB15F4997f32f9"),
                        AmountPerBlock = 1000000,
                        StartBlock = 0,
                        EndBlock = 0,
                    },
                    new GoldDistribution
                    {
                        Address = new Address("F9A15F870701268Bd7bBeA6502eB15F4997f32f9"),
                        AmountPerBlock = 100,
                        StartBlock = 1,
                        EndBlock = 100000,
                    },
                    new GoldDistribution
                    {
                        Address = new Address("Fb90278C67f9b266eA309E6AE8463042f5461449"),
                        AmountPerBlock = 3000,
                        StartBlock = 3600,
                        EndBlock = 13600,
                    },
                    new GoldDistribution
                    {
                        Address = new Address("Fb90278C67f9b266eA309E6AE8463042f5461449"),
                        AmountPerBlock = 100000000000,
                        StartBlock = 2,
                        EndBlock = 2,
                    },
                };
            }

            if (!(goldDistribution is null))
            {
                config["currency"] = new Dictionary<string, object>
                {
                    ["initialMinter"] = ByteUtil.Hex(_privateKey.ByteArray),
                    ["initialCurrencyDeposit"] = goldDistribution
                };
            }

            // AdminConfig: activate, address, validUntil
            var adminConfig = new Dictionary<string, object>
            {
                ["activate"] = true,
                ["address"] = "0000000000000000000000000000000000000005",
                ["validUntil"] = 1500000,
            };
            config["admin"] = adminConfig;

            // Serialize and write config file
            string json = JsonSerializer.Serialize(config);
            var configPath = Path.GetTempFileName();
            File.WriteAllText(configPath, json);
            if (exc)
            {
                Assert.Throws<CommandExitedException>(() => _command.Mine(configPath));
            }
            else
            {
                _command.Mine(configPath);
                File.Exists("genesis-block");
                File.Exists("keys");
            }
        }
    }
}
