using Libplanet.Types.Blocks;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Numerics;
    using NineChronicles.Headless.Executable.IO;
    using Lib9c;
    using Libplanet.Common;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model.State;
    using Lib9cUtils = Lib9c.DevExtensions.Utils;
    using CoconaUtils = Libplanet.Extensions.Cocona.Utils;

    public static class GenesisConfigExtensions
    {
        public const int DefaultCurrencyValue = 10000;

        // FIXME: Remove initialMinter.
        public static Block GenerateGenesisBlock(this GenesisConfig genesisConfig, IConsole console, out PrivateKey initialMinter)
        {
            ProcessData(console, genesisConfig.Data, out var tableSheets);

            ProcessCurrency(console, genesisConfig.Currency, out var currency, out initialMinter, out var initialDepositList);

            ProcessAdmin(console, genesisConfig.Admin, initialMinter, out var adminState, out var adminMeads);

            ProcessValidator(console, genesisConfig.InitialValidatorSet, initialMinter, out var initialValidatorSet);

            ProcessInitialMeadConfigs(console, genesisConfig.InitialMeadConfigs, out var initialMeads);

            ProcessInitialPledgeConfigs(console, genesisConfig.InitialPledgeConfigs, out var initialPledges);

            // Mine genesis block
            console.Out.WriteLine("\nMining genesis block...\n");
            return BlockHelper.ProposeGenesisBlock(
                tableSheets: tableSheets,
                goldDistributions: initialDepositList.ToArray(),
                pendingActivationStates: Array.Empty<PendingActivationState>(),
                // FIXME Should remove default value after fixing parameter type on Lib9c side.
                adminState: adminState ?? new AdminState(default, 0L),
                privateKey: initialMinter,
                initialValidators: initialValidatorSet.ToDictionary(
                    item => new PublicKey(ByteUtil.ParseHex(item.PublicKey)),
                    item => new BigInteger(item.Power)),
                actionBases: adminMeads.Concat(initialMeads).Concat(initialPledges).Concat(GetAdditionalActionBases()),
                goldCurrency: currency
            );
        }

        private static void ProcessData(IConsole console, DataConfig config, out Dictionary<string, string> tableSheets)
        {
            console.Out.WriteLine("\nProcessing data for genesis...");
            if (string.IsNullOrEmpty(config.TablePath))
            {
                throw CoconaUtils.Error("TablePath is not set.");
            }

            tableSheets = Lib9cUtils.ImportSheets(config.TablePath);
        }

        private static void ProcessCurrency(
            IConsole console,
            CurrencyConfig? config,
            out Currency currency,
            out PrivateKey initialMinter,
            out List<GoldDistribution> initialDepositList
        )
        {
            console.Out.WriteLine("\nProcessing currency for genesis...");
            if (config is null)
            {
                console.Out.WriteLine("CurrencyConfig not provided. Skip setting...");
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
                console.Out.WriteLine("Private Key not provided. Create random one...");
                initialMinter = new PrivateKey();
            }
            else
            {
                initialMinter = new PrivateKey(config.Value.InitialMinter);
            }

            initialDepositList = new List<GoldDistribution>();
            if (config.Value.InitialCurrencyDeposit is null || config.Value.InitialCurrencyDeposit.Count == 0)
            {
                console.Out.WriteLine("Initial currency deposit list not provided. " +
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

        private static void ProcessAdmin(
            IConsole console,
            AdminConfig? config,
            PrivateKey initialMinter,
            out AdminState? adminState,
            out List<ActionBase> meadActions
        )
        {
            // FIXME: If the `adminState` is not required inside `MineGenesisBlock`,
            //        this logic will be much lighter.
            console.Out.WriteLine("\nProcessing admin for genesis...");
            adminState = default;
            meadActions = new List<ActionBase>();

            if (config is null)
            {
                console.Out.WriteLine("AdminConfig not provided. Skip admin setting...");
                return;
            }

            if (config.Value.Activate)
            {
                Address adminAddress;
                if (string.IsNullOrEmpty(config.Value.Address))
                {
                    console.Out.WriteLine("Admin address not provided. Give admin privilege to initialMinter");
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
                console.Out.WriteLine("Inactivate Admin. Skip admin setting...");
            }

            console.Out.WriteLine("Admin config done");
        }

        private static void ProcessValidator(
            IConsole console,
            List<Validator>? config,
            PrivateKey initialValidator,
            out List<Validator> initialValidatorSet)
        {
            console.Out.WriteLine("\nProcessing initial validator set for genesis...");
            initialValidatorSet = new List<Validator>();
            if (config is null || config.Count == 0)
            {
                console.Out.WriteLine(
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
            console.Out.WriteLine($"Initial validator set config done: {str}");
        }

        private static void ProcessInitialMeadConfigs(
            IConsole console,
            List<MeadConfig>? configs,
            out List<PrepareRewardAssets> meadActions
        )
        {
            console.Out.WriteLine("\nProcessing initial mead distribution...");

            meadActions = new List<PrepareRewardAssets>();
            if (configs is { })
            {
                foreach (MeadConfig config in configs)
                {
                    console.Out.WriteLine($"Preparing initial {config.Amount} MEAD for {config.Address}...");
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

        private static void ProcessInitialPledgeConfigs(
            IConsole console,
            List<PledgeConfig>? configs,
            out List<CreatePledge> pledgeActions
        )
        {
            console.Out.WriteLine("\nProcessing initial pledges...");

            pledgeActions = new List<CreatePledge>();
            if (configs is { })
            {
                foreach (PledgeConfig config in configs)
                {
                    console.Out.WriteLine($"Preparing a pledge for {config.AgentAddress}...");
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
    }
}
