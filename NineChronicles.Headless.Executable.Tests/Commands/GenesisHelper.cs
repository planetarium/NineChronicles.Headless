using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public static class GenesisHelper
    {
        public static PrivateKey AdminKey = new PrivateKey();
        public static PrivateKey ValidatorKey = new PrivateKey();
        public static Block MineGenesisBlock(
            Address? targetAddress = null,
            int? targetCurrency = null,
            Dictionary<PublicKey, BigInteger>? genesisValidatorSet = null)
        {
            Dictionary<string, string> tableSheets = Lib9cUtils.ImportSheets("../../../../Lib9c/Lib9c/TableCSV");
            var goldDistributionPath = Path.GetTempFileName();
            File.WriteAllText(goldDistributionPath,
                @"Address,AmountPerBlock,StartBlock,EndBlock
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,1000000,0,0
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,100,1,100000
Fb90278C67f9b266eA309E6AE8463042f5461449,3000,3600,13600
Fb90278C67f9b266eA309E6AE8463042f5461449,100000000000,2,2
");
            if (!(targetAddress is null || targetCurrency is null))
            {
                File.AppendAllText(goldDistributionPath, $"{targetAddress.ToString()},{targetCurrency},0,0");
            }

            var privateKey = AdminKey;
            goldDistributionPath =
                goldDistributionPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var config = new Dictionary<string, object>
            {
                ["PrivateKey"] = ByteUtil.Hex(privateKey.ByteArray),
                ["AdminAddress"] = "0000000000000000000000000000000000000005",
                ["AuthorizedMinerConfig"] = new Dictionary<string, object>
                {
                    ["ValidUntil"] = 1500000,
                    ["Interval"] = 50,
                    ["Miners"] = new List<string>
                    {
                        "0000000000000000000000000000000000000001",
                        "0000000000000000000000000000000000000002",
                        "0000000000000000000000000000000000000003",
                        "0000000000000000000000000000000000000004"
                    }
                }
            };
            string json = JsonSerializer.Serialize(config);
            GenesisConfig genesisConfig = JsonSerializer.Deserialize<GenesisConfig>(json);

            Lib9cUtils.CreateActivationKey(
                out List<PendingActivationState> pendingActivationStates,
                out List<ActivationKey> activationKeys,
                (uint)config.Count);
            var authorizedMinersState = new AuthorizedMinersState(
                genesisConfig.AuthorizedMinerConfig.Miners.Select(a => new Address(a)),
                genesisConfig.AuthorizedMinerConfig.Interval,
                genesisConfig.AuthorizedMinerConfig.ValidUntil
            );
            GoldDistribution[] goldDistributions = GoldDistribution
                .LoadInDescendingEndBlockOrder(goldDistributionPath);
            AdminState adminState =
                new AdminState(new Address(genesisConfig.AdminAddress), genesisConfig.AdminValidUntil);
            Block genesisBlock = BlockHelper.ProposeGenesisBlock(
                tableSheets,
                goldDistributions,
                pendingActivationStates.ToArray(),
                adminState,
                authorizedMinersState,
                ImmutableHashSet<Address>.Empty,
                genesisValidatorSet ?? new Dictionary<PublicKey, BigInteger>
                {
                    {
                        ValidatorKey.PublicKey,
                        BigInteger.One
                    }
                },
                genesisConfig.ActivationKeyCount != 0,
                null,
                new PrivateKey(ByteUtil.ParseHex(genesisConfig.PrivateKey))
            );
            return genesisBlock;
        }

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
            public uint ActivationKeyCount { get; set; }
            public string AdminAddress { get; set; }
            public long AdminValidUntil { get; set; }
            public AuthorizedMinerConfig AuthorizedMinerConfig { get; set; }
        }
    }
}
