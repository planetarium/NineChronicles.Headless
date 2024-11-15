using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Cocona;
using Lib9c;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.IO;
using CoconaUtils = Libplanet.Extensions.Cocona.Utils;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class GenesisCommand : CoconaLiteConsoleAppBase
    {
        private const int DefaultCurrencyValue = 10000;
        private readonly IConsole _console;

        public GenesisCommand(IConsole console)
        {
            _console = console;
        }

        private void ProcessData(DataConfig config, out Dictionary<string, string> tableSheets)
        {
            _console.Out.WriteLine("\nProcessing data for genesis...");
            if (string.IsNullOrEmpty(config.TablePath))
            {
                throw CoconaUtils.Error("TablePath is not set.");
            }

            tableSheets = Lib9cUtils.ImportSheets(config.TablePath);
        }

        private void ProcessCurrency(
            CurrencyConfig? config,
            out Currency currency,
            out PrivateKey initialMinter,
            out List<GoldDistribution> initialDepositList
        )
        {
            _console.Out.WriteLine("\nProcessing currency for genesis...");
            if (config is null)
            {
                _console.Out.WriteLine("CurrencyConfig not provided. Skip setting...");
                initialMinter = new PrivateKey();
                initialDepositList = new List<GoldDistribution>
                {
                    new()
                    {
                        Address = initialMinter.Address,
                        AmountPerBlock = DefaultCurrencyValue,
                        StartBlock = 0,
                        EndBlock = 0,
                    }
                };

#pragma warning disable CS0618
                // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                currency = Currency.Legacy("NCG", 2, minters: null);
#pragma warning restore CS0618
                return;
            }

            if (string.IsNullOrEmpty(config.Value.InitialMinter))
            {
                _console.Out.WriteLine("Private Key not provided. Create random one...");
                initialMinter = new PrivateKey();
            }
            else
            {
                initialMinter = new PrivateKey(config.Value.InitialMinter);
            }

            initialDepositList = new List<GoldDistribution>();
            if (config.Value.InitialCurrencyDeposit is null || config.Value.InitialCurrencyDeposit.Count == 0)
            {
                _console.Out.WriteLine("Initial currency deposit list not provided. " +
                                       $"Give initial {DefaultCurrencyValue} currency to InitialMinter");
                initialDepositList.Add(new GoldDistribution
                {
                    Address = initialMinter.Address,
                    AmountPerBlock = DefaultCurrencyValue,
                    StartBlock = 0,
                    EndBlock = 0
                });
            }
            else
            {
                initialDepositList = config.Value.InitialCurrencyDeposit;
            }

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            currency = Currency.Legacy("NCG", 2, minters: config.Value.AllowMint ? null : ImmutableHashSet.Create(initialMinter.Address));
#pragma warning restore CS0618
        }

        private void ProcessAdmin(
            AdminConfig? config,
            PrivateKey initialMinter,
            out AdminState? adminState,
            out List<ActionBase> meadActions
        )
        {
            // FIXME: If the `adminState` is not required inside `MineGenesisBlock`,
            //        this logic will be much lighter.
            _console.Out.WriteLine("\nProcessing admin for genesis...");
            adminState = default;
            meadActions = new List<ActionBase>();

            if (config is null)
            {
                _console.Out.WriteLine("AdminConfig not provided. Skip admin setting...");
                return;
            }

            if (config.Value.Activate)
            {
                Address adminAddress;
                if (string.IsNullOrEmpty(config.Value.Address))
                {
                    _console.Out.WriteLine("Admin address not provided. Give admin privilege to initialMinter");
                    adminAddress = initialMinter.Address;
                }
                else
                {
                    adminAddress = new Address(config.Value.Address);
                }

                adminState = new AdminState(adminAddress, config.Value.ValidUntil);
                meadActions.Add(new PrepareRewardAssets
                {
                    RewardPoolAddress = adminAddress,
                    Assets = new List<FungibleAssetValue>
                    {
                        10000 * Currencies.Mead,
                    },
                });
            }
            else
            {
                _console.Out.WriteLine("Inactivate Admin. Skip admin setting...");
            }

            _console.Out.WriteLine("Admin config done");
        }

        private void ProcessValidator(List<Validator>? config, PrivateKey initialValidator,
            out List<Validator> initialValidatorSet)
        {
            _console.Out.WriteLine("\nProcessing initial validator set for genesis...");
            initialValidatorSet = new List<Validator>();
            if (config is null || config.Count == 0)
            {
                _console.Out.WriteLine(
                    "InitialValidatorSet not provided. Use initial minter as initial validator."
                );
                initialValidatorSet.Add(new Validator
                {
                    PublicKey = initialValidator.PublicKey.ToString(),
                    Power = 1,
                }
                );
            }
            else
            {
                initialValidatorSet = config.ToList();
            }

            var str = initialValidatorSet.Aggregate(string.Empty,
                (s, v) => s + "PublicKey: " + v.PublicKey + ", Power: " + v.Power + "\n");
            _console.Out.WriteLine($"Initial validator set config done: {str}");
        }

        private void ProcessInitialMeadConfigs(
            List<MeadConfig>? configs,
            out List<PrepareRewardAssets> meadActions
        )
        {
            _console.Out.WriteLine("\nProcessing initial mead distribution...");

            meadActions = new List<PrepareRewardAssets>();
            if (configs is { })
            {
                foreach (MeadConfig config in configs)
                {
                    _console.Out.WriteLine($"Preparing initial {config.Amount} MEAD for {config.Address}...");
                    Address target = new(config.Address);
                    meadActions.Add(
                        new PrepareRewardAssets(
                            target,
                            new List<FungibleAssetValue>
                            {
                                FungibleAssetValue.Parse(Currencies.Mead, config.Amount),
                            }
                        )
                    );
                }
            }
        }

        private void ProcessInitialPledgeConfigs(
            List<PledgeConfig>? configs,
            out List<CreatePledge> pledgeActions
        )
        {
            _console.Out.WriteLine("\nProcessing initial pledges...");

            pledgeActions = new List<CreatePledge>();
            if (configs is { })
            {
                foreach (PledgeConfig config in configs)
                {
                    _console.Out.WriteLine($"Preparing a pledge for {config.AgentAddress}...");
                    Address agentAddress = new(config.AgentAddress);
                    Address pledgeAddress = agentAddress.GetPledgeAddress();
                    pledgeActions.Add(
                        new CreatePledge()
                        {
                            AgentAddresses = new[] { (agentAddress, pledgeAddress) },
                            PatronAddress = new(config.PatronAddress),
                            Mead = config.Mead,
                        }
                    );
                }
            }
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
                ProcessData(genesisConfig.Data, out var tableSheets);

                ProcessCurrency(genesisConfig.Currency, out var currency, out var initialMinter, out var initialDepositList);

                ProcessAdmin(genesisConfig.Admin, initialMinter, out var adminState, out var adminMeads);

                ProcessValidator(genesisConfig.InitialValidatorSet, initialMinter, out var initialValidatorSet);

                ProcessInitialMeadConfigs(genesisConfig.InitialMeadConfigs, out var initialMeads);

                ProcessInitialPledgeConfigs(genesisConfig.InitialPledgeConfigs, out var initialPledges);

                // Mine genesis block
                _console.Out.WriteLine("\nMining genesis block...\n");
                Block block = BlockHelper.ProposeGenesisBlock(
                    validatorSet: new ValidatorSet(
                        initialValidatorSet.Select(
                            v => new Libplanet.Types.Consensus.Validator(
                                new PublicKey(ByteUtil.ParseHex(v.PublicKey)),
                                new BigInteger(v.Power))).ToList()),
                    tableSheets: tableSheets,
                    goldDistributions: initialDepositList.ToArray(),
                    pendingActivationStates: Array.Empty<PendingActivationState>(),
                    // FIXME Should remove default value after fixing parameter type on Lib9c side.
                    adminState: adminState ?? new AdminState(default, 0L),
                    privateKey: initialMinter,
                    actionBases: adminMeads.Concat(initialMeads).Concat(initialPledges).Concat(GetAdditionalActionBases()),
                    goldCurrency: currency
                );

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
                            $"{initialMinter.Address},{ByteUtil.Hex(initialMinter.ByteArray)},{DefaultCurrencyValue},0,0");
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

        /// <summary>
        /// Actions to be appended on end of transaction actions.
        /// You can add actions code to this method before generate genesis block.
        /// </summary>
        /// <returns>List of ActionBase.</returns>
        private static List<ActionBase> GetAdditionalActionBases()
        {
            return new List<ActionBase>
            {

            };
        }

#pragma warning disable S3459
        /// <summary>
        /// Game data to set into genesis block.
        /// </summary>
        /// <seealso cref="GenesisConfig"/>
        [Serializable]
        private struct DataConfig
        {
            /// <value>A path of game data table directory.</value>
            public string TablePath { get; set; }
        }

        /// <summary>
        /// Currency related configurations.<br/>
        /// Set initial minter(Tx signer) and/or initial currency depositions.<br/>
        /// If not provided, default values will set.
        /// </summary>
        [Serializable]
        private struct CurrencyConfig
        {
            /// <value>
            /// Private Key of initial currency minter.<br/>
            /// If not provided, a new private key will be created and used.<br/>
            /// </value>
            public string? InitialMinter { get; set; } // PrivateKey, not Address

            /// <value>
            /// Initial currency deposition list.<br/>
            /// If you leave it to empty list or even not provide, the `InitialMinter` will get 10000 currency.<br.>
            /// You can see newly created deposition info in <c>initial_deposit.csv</c> file.
            /// </value>
            public List<GoldDistribution>? InitialCurrencyDeposit { get; set; }

            public bool AllowMint { get; set; }
        }

        /// <summary>
        /// Admin related configurations.<br/>
        /// If not provided, no admin will be set.
        /// </summary>
        [Serializable]
        private struct AdminConfig
        {
            /// <value>Whether active admin address or not.</value>
            public bool Activate { get; set; }

            /// <value>
            /// Address to give admin privilege.<br/>
            /// If <c>Activate</c> is <c>true</c> and no <c>Address</c> provided, the <see cref="CurrencyConfig.InitialMinter"/> will get admin privilege.
            /// </value>
            public string Address { get; set; }

            /// <value>
            /// The block count to persist admin privilege.<br/>
            /// After this block, admin will no longer be admin.
            /// </value>
            public long ValidUntil { get; set; }
        }

        [Serializable]
        private struct Validator
        {
            public string PublicKey { get; set; }

            public long Power { get; set; }
        }

        [Serializable]
        private struct MeadConfig
        {
            public string Address { get; set; }

            public string Amount { get; set; }
        }

        [Serializable]
        private struct PledgeConfig
        {
            public string AgentAddress { get; set; }

            public string PatronAddress { get; set; }

            public int Mead { get; set; }
        }

        /// <summary>
        /// Config to mine new genesis block.
        /// </summary>
        /// <list type="table">
        /// <listheader>
        /// <term>Config</term>
        /// <description>Description</description>
        /// </listheader>
        /// <item>
        /// <term><see cref="DataConfig">Data</see></term>
        /// <description>Required. Sets game data to genesis block.</description>
        /// </item>
        /// <item>
        /// <term><see cref="CurrencyConfig">Currency</see></term>
        /// <description>Optional. Sets initial currency mint/deposition data to genesis block.</description>
        /// </item>
        /// <item>
        /// <term><see cref="AdminConfig">Admin</see></term>
        /// <description>Optional. Sets game admin and lifespan to genesis block.</description>
        /// </item>
        /// <item>
        /// <term><see cref="InitialValidatorSet">Initial validator set</see></term>
        /// <description>Optional. Sets game admin and lifespan to genesis block.</description>
        /// </item>
        /// </list>
        [Serializable]
        private struct GenesisConfig
        {
            public DataConfig Data { get; set; } // Required
            public CurrencyConfig? Currency { get; set; }
            public AdminConfig? Admin { get; set; }
            public List<Validator>? InitialValidatorSet { get; set; }

            public List<MeadConfig>? InitialMeadConfigs { get; set; }

            public List<PledgeConfig>? InitialPledgeConfigs { get; set; }
        }
#pragma warning restore S3459
    }
}
