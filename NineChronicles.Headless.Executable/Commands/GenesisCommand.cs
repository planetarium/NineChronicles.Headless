using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bencodex;
using Cocona;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.IO;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class GenesisCommand : CoconaLiteConsoleAppBase
    {
        private static readonly Codec Codec = new Codec();
        private readonly IConsole _console;

        public GenesisCommand(IConsole console)
        {
            _console = console;
        }

        [Command(Description = "Mine a new genesis block.")]
        public void Mine(
            [Argument("CONFIG", Description = "JSON config path to mine genesis block.")]
            string configPath
        )
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            string json = File.ReadAllText(configPath);
            GenesisConfig genesisConfig = JsonSerializer.Deserialize<GenesisConfig>(json, options);
            if (string.IsNullOrEmpty(genesisConfig.TablePath))
            {
                throw Utils.Error("TablePath is not set.");
            }

            Dictionary<string, string> tableSheets = Lib9cUtils.ImportSheets(genesisConfig.TablePath);

            Lib9cUtils.CreateActivationKey(
                out List<PendingActivationState> pendingActivationStates,
                out List<ActivationKey> activationKeys,
                genesisConfig.ActivationKeyCount);

            if (string.IsNullOrEmpty(genesisConfig.PrivateKey))
            {
                throw Utils.Error("PrivateKey is not set");
            }

            if (string.IsNullOrEmpty(genesisConfig.GoldDistributionPath))
            {
                throw Utils.Error("GoldDistributionPath is not set");
            }

            try
            {
                GoldDistribution[] goldDistributions = GoldDistribution
                    .LoadInDescendingEndBlockOrder(genesisConfig.GoldDistributionPath);

                AdminState adminState =
                    new AdminState(new Address(genesisConfig.AdminAddress), genesisConfig.AdminValidUntil);

                AuthorizedMinersState? authorizedMinersState = null;
                if (genesisConfig.AuthorizedMinerConfig.Miners.Any())
                {
                    authorizedMinersState = new AuthorizedMinersState(
                        miners: genesisConfig.AuthorizedMinerConfig.Miners.Select(a => new Address(a)),
                        interval: genesisConfig.AuthorizedMinerConfig.Interval,
                        validUntil: genesisConfig.AuthorizedMinerConfig.ValidUntil
                    );
                }

                Block<PolymorphicAction<ActionBase>> block = BlockHelper.MineGenesisBlock(
                    tableSheets,
                    goldDistributions,
                    pendingActivationStates.ToArray(),
                    adminState,
                    authorizedMinersState,
                    ImmutableHashSet<Address>.Empty,
                    genesisConfig.ActivationKeyCount != 0,
                    null,
                    new PrivateKey(ByteUtil.ParseHex(genesisConfig.PrivateKey))
                );

                Lib9cUtils.ExportBlock(block, "genesis-block");
                Lib9cUtils.ExportKeys(activationKeys, "keys");
            }
            catch (Exception e)
            {
                throw Utils.Error(e.Message);
            }
        }

#pragma warning disable S3459
        [Serializable]
        private struct AuthorizedMinerConfig
        {
            public long Interval { get; set; }
            public long ValidUntil { get; set; }
            public List<string> Miners { get; set; }
        }

        [Serializable]
        private struct GenesisConfig
        {
            public string PrivateKey { get; set; }
            public string TablePath { get; set; }
            public string GoldDistributionPath { get; set; }
            public uint ActivationKeyCount { get; set; }
            public string AdminAddress { get; set; }
            public long AdminValidUntil { get; set; }
            public AuthorizedMinerConfig AuthorizedMinerConfig { get; set; }
            public List<string> ActivatedAccounts { get; set; }
        }
#pragma warning restore S3459
    }
}
