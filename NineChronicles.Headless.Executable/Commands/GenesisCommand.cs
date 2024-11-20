using System;
using System.IO;
using System.Text.Json;
using Cocona;
using Libplanet.Common;
using NineChronicles.Headless.Executable.IO;
using CoconaUtils = Libplanet.Extensions.Cocona.Utils;
using NineChronicles.Headless.Executable.Models.Genesis;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class GenesisCommand : CoconaLiteConsoleAppBase
    {
        private readonly IConsole _console;

        public GenesisCommand(IConsole console)
        {
            _console = console;
        }


        [Command(Description = "Mine a new genesis block")]
        public void Mine(
            [Argument("CONFIG", Description = "JSON config path to mine genesis block")]
            string configPath = "./config.json")
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            string json = File.ReadAllText(configPath);
            GenesisConfig genesisConfig = JsonSerializer.Deserialize<GenesisConfig>(json, options);

            try
            {
                var block = genesisConfig.GenerateGenesisBlock(_console, out var initialMinter);
                Lib9cUtils.ExportBlock(block, "genesis-block");
                if (genesisConfig.Admin?.Activate == true)
                {
                    if (string.IsNullOrEmpty(genesisConfig.Admin.Value.Address))
                    {
                        _console.Out.WriteLine("Initial minter has admin privilege. Keep this account in secret.");
                    }
                    else
                    {
                        _console.Out.WriteLine("Admin privilege has been granted to given admin address. " +
                                               "Keep this account in secret.");
                    }
                }

                if (genesisConfig.Currency?.InitialCurrencyDeposit is null ||
                    genesisConfig.Currency.Value.InitialCurrencyDeposit.Count == 0)
                {
                    if (string.IsNullOrEmpty(genesisConfig.Currency?.InitialMinter))
                    {
                        _console.Out.WriteLine("No currency data provided. Initial minter gets initial deposition.\n" +
                                               "Please check `initial_deposit.csv` file to get detailed info.");
                        File.WriteAllText("initial_deposit.csv",
                            "Address,PrivateKey,AmountPerBlock,StartBlock,EndBlock\n");
                        File.AppendAllText("initial_deposit.csv",
                            $"{initialMinter.Address},{ByteUtil.Hex(initialMinter.ByteArray)},{GenesisConfigExtensions.DefaultCurrencyValue},0,0");
                    }
                    else
                    {
                        _console.Out.WriteLine("No initial deposit data provided. " +
                                               "Initial minter you provided gets initial deposition.");
                    }
                }

                _console.Out.WriteLine("\nGenesis block created.");
            }
            catch (Exception e)
            {
                throw CoconaUtils.Error(e.Message);
            }
        }
    }
}
