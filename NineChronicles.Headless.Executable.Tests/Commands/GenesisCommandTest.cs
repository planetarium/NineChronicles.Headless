using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Cocona;
using Libplanet;
using Libplanet.Blocks;
using Libplanet.Crypto;
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
        [InlineData(true, true, false, false)]
        [InlineData(true, true, true, false)]
        [InlineData(true, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, false, true)]
        public void Mine(bool tablePathExist, bool goldDistributionPathExist, bool actionsExist, bool exc)
        {
            var tablePath = tablePathExist
                ? Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Lib9c", "Lib9c", "TableCSV"))
                : "";

            var goldDistributionPath = "";
            if (goldDistributionPathExist)
            {
                goldDistributionPath = Path.GetTempFileName();
                File.WriteAllText(goldDistributionPath, @"Address,AmountPerBlock,StartBlock,EndBlock
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,1000000,0,0
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,100,1,100000
Fb90278C67f9b266eA309E6AE8463042f5461449,3000,3600,13600
Fb90278C67f9b266eA309E6AE8463042f5461449,100000000000,2,2
");
            }

            var actions = new List<string>();
            if (actionsExist)
            {
                var actionCommand = new ActionCommand(_console);
                for (int i = 0; i < 2; i++)
                {
                    var path = Path.GetTempFileName();
                    actionCommand.MonsterCollect(i, path);
                    actions.Add(path);
                }
            }
            // Avoid JsonException in Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (tablePathExist)
                {
                    tablePath = tablePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                if (goldDistributionPathExist)
                {
                    goldDistributionPath = goldDistributionPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }

            var config = new Dictionary<string, object>
            {
                ["privateKey"] = ByteUtil.Hex(_privateKey.ByteArray),
                ["tablePath"] = tablePath,
                ["goldDistributionPath"] = goldDistributionPath,
                ["adminAddress"] = "0000000000000000000000000000000000000005",
                ["authorizedMinerConfig"] = new Dictionary<string, object>
                {
                    ["validUntil"] = 1500000,
                    ["interval"] = 50,
                    ["miners"] = new List<string>
                    {
                        "0000000000000000000000000000000000000001",
                        "0000000000000000000000000000000000000002",
                        "0000000000000000000000000000000000000003",
                        "0000000000000000000000000000000000000004"
                    }
                }
            };
            if (actionsExist)
            {
                config["actions"] = actions;
            }
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
