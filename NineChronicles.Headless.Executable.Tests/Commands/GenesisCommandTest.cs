using System.IO;
using System.Runtime.InteropServices;
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
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(false, false, true)]
        public void Mine(bool tablePathExist, bool goldDistributionPathExist, bool exc)
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

            var json = $@" {{
                 ""privateKey"": ""{ByteUtil.Hex(_privateKey.ByteArray)}"",
                 ""tablePath"": ""{tablePath}"",
                 ""goldDistributionPath"": ""{goldDistributionPath}"",
                 ""adminAddress"": ""0000000000000000000000000000000000000005"",
                 ""authorizedMinerConfig"": {{
                 ""validUntil"": 1500000,
                 ""interval"": 50,
                 ""miners"": [
                     ""0000000000000000000000000000000000000001"",
                     ""0000000000000000000000000000000000000002"",
                     ""0000000000000000000000000000000000000003"",
                     ""0000000000000000000000000000000000000004""
                 ] }}}}";
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
