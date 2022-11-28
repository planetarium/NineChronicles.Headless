using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Extensions.Cocona;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.BlockChain.Policy;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
using Serilog.Core;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ChainCommandTest : IDisposable
    {
        // `StoreType.Memory` will not be tested.
        // Because the purpose of ChainCommandTest is to store and read blockchain data.
        private readonly StringIOConsole _console;
        private readonly ChainCommand _command;

        private readonly string _storePath;

        public ChainCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ChainCommand(_console);

            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Tip(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.ProposeGenesisBlock();
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            store.Dispose();

            // FIXME For an unknown reason, BlockHeader.TimeStamp precision issue occurred and the store we should open it again.
            store = storeType.CreateStore(_storePath);
            genesisBlock = store.GetBlock<NCAction>(genesisBlock.Hash);
            store.Dispose();

            _command.Tip(storeType, _storePath);
            Assert.Equal(
                Utils.SerializeHumanReadable(genesisBlock.Header),
                _console.Out.ToString().Trim()
            );
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Inspect(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.ProposeGenesisBlock();
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateStore = new TrieStateStore(new RocksDBKeyValueStore(Path.Combine(_storePath, "states")));

            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetTestPolicy();
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);

            var action = new HackAndSlash
            {
                Costumes = new List<Guid>(),
                Equipments = new List<Guid>(),
                Foods = new List<Guid>(),
                WorldId = 1,
                StageId = 1,
                AvatarAddress = default
            };

            var minerKey = new PrivateKey();
            chain.MakeTransaction(minerKey, new PolymorphicAction<ActionBase>[] { action });
            Block<PolymorphicAction<ActionBase>> block = chain.ProposeBlock(minerKey, DateTimeOffset.Now);
            chain.Append(block, GenerateBlockCommit(block));
            store.Dispose();
            stateStore.Dispose();

            _command.Inspect(storeType, _storePath);
            List<double> output = _console.Out.ToString().Split("\n")[1]
                .Split(',').Select(double.Parse).ToList();
            var totalTxCount = Convert.ToInt32(output[2]);
            var hackandslashCount = Convert.ToInt32(output[3]);

            Assert.Equal(1, totalTxCount);
            Assert.Equal(1, hackandslashCount);
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Truncate(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.ProposeGenesisBlock();
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateStore = new TrieStateStore(new RocksDBKeyValueStore(Path.Combine(_storePath, "states")));

            var validators = new ValidatorSet(new List<PublicKey> { ValidatorsPolicy.TestValidatorKey.PublicKey });

            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<NCAction>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicy<NCAction>(getValidatorSet: index => validators);
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);

            var action = new HackAndSlash
            {
                Costumes = new List<Guid>(),
                Equipments = new List<Guid>(),
                Foods = new List<Guid>(),
                WorldId = 1,
                StageId = 1,
                AvatarAddress = default
            };

            var minerKey = new PrivateKey();
            for (var i = 0; i < 2; i++)
            {
                chain.MakeTransaction(minerKey, new NCAction[] { action });
                if (chain.Tip.Index < 1)
                {
                    Block<NCAction> block = chain.ProposeBlock(minerKey, DateTimeOffset.Now);
                    chain.Append(block, GenerateBlockCommit(block));
                }
                else
                {
                    Block<NCAction> block = chain.ProposeBlock(
                        minerKey,
                        DateTimeOffset.Now,
                        lastCommit: GenerateBlockCommit(chain.Tip));
                    chain.Append(block, GenerateBlockCommit(block));
                }
            }

            var indexCountBeforeTruncate = store.CountIndex(chainId);
            store.Dispose();
            stateStore.Dispose();
            _command.Truncate(storeType, _storePath, 1);
            IStore storeAfterTruncate = storeType.CreateStore(_storePath);
            chainId = storeAfterTruncate.GetCanonicalChainId() ?? new Guid();
            var indexCountAfterTruncate = storeAfterTruncate.CountIndex(chainId);
            storeAfterTruncate.Dispose();

            Assert.Equal(3, indexCountBeforeTruncate);
            Assert.Equal(2, indexCountAfterTruncate);
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void PruneState(StoreType storeType)
        {
            IStore store = storeType.CreateStore(_storePath);
            var statesPath = Path.Combine(_storePath, "states");
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            var genesisBlock = MineGenesisBlock();
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<NCAction>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);
            chain.ExecuteActions(chain.Tip);
            int prevStatesCount = stateKeyValueStore.ListKeys().Count();
            stateKeyValueStore.Set(
                new KeyBytes("alpha", Encoding.UTF8),
                ByteUtil.ParseHex("00"));
            stateKeyValueStore.Set(
                new KeyBytes("beta", Encoding.UTF8),
                ByteUtil.ParseHex("00"));
            Assert.Equal(prevStatesCount + 2, stateKeyValueStore.ListKeys().Count());
            store.Dispose();
            stateStore.Dispose();
            _command.PruneStates(storeType, _storePath);
            IStore outputStore = storeType.CreateStore(_storePath);
            var outputStateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var outputStateStore = new TrieStateStore(outputStateKeyValueStore, true);
            int outputStatesCount = outputStateKeyValueStore.ListKeys().Count();
            outputStore.Dispose();
            outputStateStore.Dispose();
            Assert.Equal(prevStatesCount, outputStatesCount);
        }

        private Block<NCAction> MineGenesisBlock()
        {
            Dictionary<string, string> tableSheets = Lib9cUtils.ImportSheets("../../../../Lib9c/Lib9c/TableCSV");
            var goldDistributionPath = Path.GetTempFileName();
            File.WriteAllText(goldDistributionPath, @"Address,AmountPerBlock,StartBlock,EndBlock
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,1000000,0,0
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,100,1,100000
Fb90278C67f9b266eA309E6AE8463042f5461449,3000,3600,13600
Fb90278C67f9b266eA309E6AE8463042f5461449,100000000000,2,2
");
            var privateKey = new PrivateKey();
            goldDistributionPath = goldDistributionPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            Block<NCAction> genesisBlock = BlockHelper.ProposeGenesisBlock(
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
            return genesisBlock;
        }

        private BlockCommit? GenerateBlockCommit<T>(Block<T> block)
            where T : IAction, new()
        {
            return block.Index != 0
                ? new BlockCommit(
                    block.Index,
                    0,
                    block.Hash,
                    ImmutableArray<Vote>.Empty.Add(new VoteMetadata(
                        block.Index,
                        0,
                        block.Hash,
                        DateTimeOffset.UtcNow,
                        ValidatorsPolicy.TestValidatorKey.PublicKey,
                        VoteFlag.PreCommit).Sign(ValidatorsPolicy.TestValidatorKey)))
                : null;
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

        public void Dispose()
        {
            if (Directory.Exists(_storePath))
            {
                Directory.Delete(_storePath, true);
            }
        }
    }
}
