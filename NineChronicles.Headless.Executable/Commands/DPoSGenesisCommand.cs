using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Cocona;
using Libplanet.Crypto;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Nekoyume;
using NineChronicles.Headless.Executable.IO;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Commands;

public class DPoSGenesisCommand
{
    private readonly IConsole _console;
    
    public DPoSGenesisCommand(IConsole console)
    {
        _console = console;
    }

    [Command(Description = "Mine a new dpos-based genesis block")]
    public void Mine(
        [Argument("STORE", Description = "Store path to set initial state")]
        string storePath,
        [Argument("CONFIG", Description = "JSON config path to mine genesis block")]
        string configPath)
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        string json = File.ReadAllText(configPath);
        DPoSGenesisConfig genesisConfig = JsonSerializer.Deserialize<DPoSGenesisConfig>(json, options);
        var privateKey = new PrivateKey(genesisConfig.Proposer);
        var initialNCGs = genesisConfig.InitialAssets ?? new List<AssetConfig>();
        var validators = genesisConfig.InitialValidators ?? new List<ValidatorConfig>();
        string stateStorePath = Path.Combine(storePath, "states");
        IStateStore stateStore = new TrieStateStore(new RocksDBKeyValueStore(stateStorePath));
        var block = DPoSBlockHelper.ProposeGenesisBlock(
            privateKey,
            stateStore,
            initialNCGs.ToDictionary(
                v => new Address(v.Address),
                v => (BigInteger)v.Amount),
            validators.ToDictionary(
                v => PublicKey.FromHex(v.PublicKey),
                v => (BigInteger)v.Power));
        Lib9cUtils.ExportBlock(block, "genesis-block");
    }
    
#pragma warning disable S3459
    [Serializable]
    private struct AssetConfig
    {
        public string Address { get; set; }

        public long Amount { get; set; }
    }

    [Serializable]
    private struct ValidatorConfig
    {
        public string PublicKey { get; set; }

        public long Power { get; set; }
    }

    [Serializable]
    private struct DPoSGenesisConfig
    {
        public string Proposer { get; set; } // Required

        public List<AssetConfig>? InitialAssets { get; set; }

        public List<ValidatorConfig>? InitialValidators { get; set; }
    }
#pragma warning restore S3459
}
