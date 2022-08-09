using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.IO;
using Serilog;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class GenesisCommand : CoconaLiteConsoleAppBase
    {
        private const int DefaultCurrencyValue = 10000;
        private static readonly Codec _codec = new Codec();
        private readonly IConsole _console;

        public GenesisCommand(IConsole console)
        {
            _console = console;
        }

        private void ProcessData(DataConfig config, out Dictionary<string, string> tableSheets)
        {
            Console.WriteLine("Processing data for genesis...");
            if (string.IsNullOrEmpty(config.TablePath))
            {
                throw Utils.Error("TablePath is not set.");
            }

            tableSheets = Lib9cUtils.ImportSheets(config.TablePath);
        }

        private void ProcessCurrency(
            CurrencyConfig? config,
            out PrivateKey initialMinter,
            out List<GoldDistribution> initialDepositList
        )
        {
            Console.WriteLine("Processing currency for genesis...");
            if (config is null)
            {
                Log.Information("CurrencyConfig not provided. Skip setting...");
                initialMinter = new PrivateKey();
                initialDepositList = new List<GoldDistribution>
                {
                    new()
                    {
                        Address = initialMinter.ToAddress(), AmountPerBlock = DefaultCurrencyValue,
                        StartBlock = 0, EndBlock = 0
                    }
                };
                return;
            }

            if (string.IsNullOrEmpty(config.Value.InitialMinter))
            {
                Log.Information("Private Key not provided. Create random one...");
                initialMinter = new PrivateKey();
            }
            else
            {
                initialMinter = new PrivateKey(config.Value.InitialMinter);
            }

            initialDepositList = new List<GoldDistribution>();
            if (config.Value.InitialCurrencyDeposit.Count == 0)
            {
                Log.Information("Initial currency deposit list not provided. " +
                                $"Give initial ${DefaultCurrencyValue} currency to InitialMinter");
                initialDepositList.Add(new GoldDistribution
                {
                    Address = initialMinter.ToAddress(),
                    AmountPerBlock = DefaultCurrencyValue,
                    StartBlock = 0,
                    EndBlock = 0
                });
            }
            else
            {
                initialDepositList = config.Value.InitialCurrencyDeposit;
            }
        }

        private void ProcessAdmin(AdminConfig? config, PrivateKey initialMinter, out AdminState adminState)
        {
            Console.WriteLine("Processing admin for genesis...");
            // FIXME: If the `adminState` is not required inside `MineGenesisBlock`,
            //        this logic will be much lighter.
            adminState = new AdminState(new PrivateKey().ToAddress(), 0);
            if (config is null)
            {
                Log.Information("AdminConfig not provided. Skip admin setting...");
                return;
            }

            if (config.Value.Activate)
            {
                if (string.IsNullOrEmpty(config.Value.Address))
                {
                    Log.Information("Admin address not provided. Give admin privilege to initialMinter");
                    adminState = new AdminState(initialMinter.ToAddress(), config.Value.ValidUntil);
                }
            }
            else
            {
                Log.Information("Inactivate Admin. Skip admin setting...");
            }

            Log.Information("Admin config done");
        }

        private void ProcessExtra(ExtraConfig? config,
            out List<PendingActivationState> pendingActivationStates
        )
        {
            Console.WriteLine("Processing extra data for genesis...");
            pendingActivationStates = new List<PendingActivationState>();

            if (config is null)
            {
                Log.Information("Extra config not provided");
                return;
            }

            if (!string.IsNullOrEmpty(config.Value.PendingActivationStatePath))
            {
                string hex = File.ReadAllText(config.Value.PendingActivationStatePath).Trim();
                List decoded = (List)_codec.Decode(ByteUtil.ParseHex(hex));
                CreatePendingActivations action = new();
                action.LoadPlainValue(decoded[1]);
                pendingActivationStates = action.PendingActivations.Select(
                    pa => new PendingActivationState(pa.Nonce, new PublicKey(pa.PublicKey))
                ).ToList();
            }
        }

        [Command(Description = "Mine a new genesis block")]
        public void Mine(
            [Argument("CONFIG", Description = "JSON config path to mine genesis block")]
            string configPath)
        {
            var loggerConf = new LoggerConfiguration();
            Log.Logger = loggerConf.CreateLogger();

            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            string json = File.ReadAllText(configPath);
            GenesisConfig genesisConfig = JsonSerializer.Deserialize<GenesisConfig>(json, options);

            try
            {
                ProcessData(genesisConfig.Data, out var tableSheets);

                ProcessCurrency(genesisConfig.Currency, out var initialMinter, out var initialDepositList);

                ProcessAdmin(genesisConfig.Admin, initialMinter, out var adminState);

                ProcessExtra(genesisConfig.Extra, out List<PendingActivationState> pendingActivationStates);

                // Mine genesis block
                Console.WriteLine("\nMining genesis block...\n");
                Block<PolymorphicAction<ActionBase>> block = BlockHelper.MineGenesisBlock(
                    tableSheets: tableSheets,
                    goldDistributions: initialDepositList.ToArray(),
                    pendingActivationStates: pendingActivationStates.ToArray(),
                    adminState: adminState,
                    privateKey: initialMinter
                );

                Lib9cUtils.ExportBlock(block, "genesis-block");
                if (genesisConfig.Admin?.Activate == true)
                {
                    if (string.IsNullOrEmpty(genesisConfig.Admin.Value.Address))
                    {
                        Console.WriteLine("Initial minter has admin privilege. Keep this account in secret.");
                    }
                    else
                    {
                        Console.WriteLine("Admin privilege has been granted to given admin address. " +
                                          "Keep this account in secret.");
                    }
                }

                if (genesisConfig.Currency?.InitialCurrencyDeposit.Count == 0)
                {
                    if (string.IsNullOrEmpty(genesisConfig.Currency?.InitialMinter))
                    {
                        Console.WriteLine("No currency data provided. Initial minter gets initial deposition.\n" +
                                          "Please check `initial_deposit.csv` file to get detailed info.");
                        File.WriteAllText("initial_deposit.csv",
                            "Address,PrivateKey,AmountPerBlock,StartBlock,EndBlock\n");
                        File.AppendAllText("initial_deposit.csv",
                            $"{initialMinter.ToAddress()},{ByteUtil.Hex(initialMinter.ByteArray)},{DefaultCurrencyValue},0,0");
                    }
                    else
                    {
                        Console.WriteLine("No initial deposit data provided. " +
                                          "Initial minter you provided gets initial deposition.");
                    }
                }

                Console.WriteLine("\nGenesis block created.");
            }
            catch (Exception e)
            {
                throw Utils.Error(e.Message);
            }
        }

#pragma warning disable S3459
        [Serializable]
        private struct DataConfig
        {
            public string TablePath { get; set; }
        }

        [Serializable]
        private struct CurrencyConfig
        {
            public string InitialMinter { get; set; } // PrivateKey, not Address
            public List<GoldDistribution> InitialCurrencyDeposit { get; set; }
        }

        [Serializable]
        private struct AdminConfig
        {
            public bool Activate { get; set; }
            public string Address { get; set; }
            public long ValidUntil { get; set; }
        }

        [Serializable]
        private struct ExtraConfig
        {
            public string? PendingActivationStatePath { get; set; }
        }

        // Config to mine new genesis block.
        [Serializable]
        private struct GenesisConfig
        {
            public DataConfig Data { get; set; } // Required
            public CurrencyConfig? Currency { get; set; }
            public AdminConfig? Admin { get; set; }

            public ExtraConfig? Extra { get; set; }
        }
#pragma warning restore S3459
    }
}
