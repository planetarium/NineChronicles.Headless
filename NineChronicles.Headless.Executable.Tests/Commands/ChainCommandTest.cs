using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Consensus;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Blockchain.Policy;
using Nekoyume.Model;
using Nekoyume.Model.State;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
using Serilog.Core;
using Xunit;
using Lib9cUtils = Lib9c.DevExtensions.Utils;
using CoconaUtils = Libplanet.Extensions.Cocona.Utils;
using Libplanet.Types.Assets;
using Nekoyume.TableData;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ChainCommandTest : IDisposable
    {
        // `StoreType.Memory` will not be tested.
        // Because the purpose of ChainCommandTest is to store and read blockchain data.
        private readonly StringIOConsole _console;
        private readonly ChainCommand _command;
        private readonly Dictionary<string, string> _sheets;

        private readonly string _storePath;

        public ChainCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ChainCommand(_console);

            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _sheets = TableSheetsImporter.ImportSheets();
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Tip(StoreType storeType)
        {
            Block genesisBlock = BlockChain.ProposeGenesisBlock();
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            store.Dispose();

            // FIXME For an unknown reason, BlockHeader.TimeStamp precision issue occurred and the store we should open it again.
            store = storeType.CreateStore(_storePath);
            genesisBlock = store.GetBlock(genesisBlock.Hash);
            store.Dispose();

            _command.Tip(storeType, _storePath);
            Assert.Equal(
                CoconaUtils.SerializeHumanReadable(genesisBlock.Header),
                _console.Out.ToString().Trim()
            );
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Inspect(StoreType storeType)
        {
            var proposer = new PrivateKey();
            IStore store = storeType.CreateStore(_storePath);
            IStateStore stateStore = new TrieStateStore(new RocksDBKeyValueStore(Path.Combine(_storePath, "states")));
            IStagePolicy stagePolicy = new VolatileStagePolicy();
            IBlockPolicy blockPolicy = new BlockPolicySource().GetPolicy();
            ActionEvaluator actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            var sheets = TableSheetsImporter.ImportSheets();
            Block genesisBlock = BlockChain.ProposeGenesisBlock(
                transactions: new IAction[]
                    {
                        new InitializeStates(
                            validatorSet: new ValidatorSet(new List<Validator>
                            {
                                new Validator(proposer.PublicKey, 10_000_000_000_000_000_000)
                            }),
                            rankingState: new RankingState0(),
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(sheets[nameof(GameConfigSheet)]),
                            redeemCodeState: new RedeemCodeState(
                                Bencodex.Types.Dictionary.Empty
                                    .Add("address", RedeemCodeState.Address.Serialize())
                                    .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            activatedAccountsState: new ActivatedAccountsState(),
                            goldCurrencyState: new GoldCurrencyState(Currency.Uncapped("ncg", 2, null)),
                            goldDistributions: new GoldDistribution[] { },
                            tableSheets: sheets,
                            pendingActivationStates: new PendingActivationState[] { }
                        )
                    }.Select((sa, nonce) => Transaction.Create(nonce, new PrivateKey(), null, new[] { sa.PlainValue }))
                    .ToImmutableList());
            BlockChain chain = BlockChain.Create(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);

            var action = new HackAndSlash
            {
                Costumes = new List<Guid>(),
                Equipments = new List<Guid>(),
                Foods = new List<Guid>(),
                WorldId = 1,
                StageId = 1,
                AvatarAddress = default,
                RuneInfos = new List<RuneSlotInfo>(),
            };

            chain.MakeTransaction(proposer, new ActionBase[] { action });
            Block block = chain.ProposeBlock(proposer);
            chain.Append(block, GenerateBlockCommit(block, proposer));
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
            var proposer = new PrivateKey();
            IStore store = storeType.CreateStore(_storePath);
            IStateStore stateStore = new TrieStateStore(new RocksDBKeyValueStore(Path.Combine(_storePath, "states")));
            IStagePolicy stagePolicy = new VolatileStagePolicy();
            IBlockPolicy blockPolicy = new BlockPolicySource().GetPolicy();
            ActionEvaluator actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            var validatorSet = new ValidatorSet(
                new[] { new Validator(proposer.PublicKey, 10_000_000_000_000_000_000) }.ToList());
            var gameConfigState = new GameConfigState(_sheets[nameof(GameConfigSheet)]);
            var redeemCodeListSheet = new RedeemCodeListSheet();
            Block genesisBlock = BlockChain.ProposeGenesisBlock(
                transactions: new IAction[]
                    {
                        new InitializeStates(
                            validatorSet: validatorSet,
                            rankingState: new RankingState0(),
                            shopState: new ShopState(),
                            tableSheets: _sheets,
                            gameConfigState: gameConfigState,
                            redeemCodeState: new RedeemCodeState(redeemCodeListSheet),
                            adminAddressState: null,
                            activatedAccountsState: new ActivatedAccountsState(ImmutableHashSet<Address>.Empty),
                            goldCurrencyState: new GoldCurrencyState(Currency.Uncapped("ncg", 2, null), 0),
                            goldDistributions: Array.Empty<GoldDistribution>(),
                            pendingActivationStates: Array.Empty<PendingActivationState>())
                    }.Select((sa, nonce) => Transaction.Create(nonce, new PrivateKey(), null, new[] { sa.PlainValue }))
                    .ToImmutableList());
            BlockChain chain = BlockChain.Create(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            Guid chainId = chain.Id;

            var action = new HackAndSlash
            {
                Costumes = new List<Guid>(),
                Equipments = new List<Guid>(),
                Foods = new List<Guid>(),
                WorldId = 1,
                StageId = 1,
                AvatarAddress = default,
                RuneInfos = new List<RuneSlotInfo>(),
            };


            for (var i = 0; i < 2; i++)
            {
                chain.MakeTransaction(proposer, new ActionBase[] { action });
                if (chain.Tip.Index < 1)
                {
                    Block block = chain.ProposeBlock(proposer);
                    chain.Append(block, GenerateBlockCommit(block, proposer));
                }
                else
                {
                    Block block = chain.ProposeBlock(
                        proposer,
                        lastCommit: GenerateBlockCommit(chain.Tip, proposer));
                    chain.Append(block, GenerateBlockCommit(block, proposer));
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
            var genesisBlock = MineGenesisBlock();
            var stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            IStagePolicy stagePolicy = new VolatileStagePolicy();
            IBlockPolicy blockPolicy = new BlockPolicySource().GetPolicy();
            ActionEvaluator actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            BlockChain chain = BlockChain.Create(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);

            // Additional pruning is now required since in-between commits are made
            store.Dispose();
            stateStore.Dispose();
            _command.PruneStates(storeType, _storePath);
            store = storeType.CreateStore(_storePath);
            stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            stateStore = new TrieStateStore(stateKeyValueStore);
            int prevStatesCount = stateKeyValueStore.ListKeys().Count();

            stateKeyValueStore.Set(
                new KeyBytes("alpha"),
                ByteUtil.ParseHex("00"));
            stateKeyValueStore.Set(
                new KeyBytes("beta"),
                ByteUtil.ParseHex("00"));
            Assert.Equal(prevStatesCount + 2, stateKeyValueStore.ListKeys().Count());

            store.Dispose();
            stateStore.Dispose();
            _command.PruneStates(storeType, _storePath);
            IStore outputStore = storeType.CreateStore(_storePath);
            var outputStateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var outputStateStore = new TrieStateStore(outputStateKeyValueStore);
            int outputStatesCount = outputStateKeyValueStore.ListKeys().Count();
            outputStore.Dispose();
            outputStateStore.Dispose();
            Assert.Equal(prevStatesCount, outputStatesCount);
        }

        [Theory]
        [InlineData(StoreType.RocksDb)]
        public void Snapshot(StoreType storeType)
        {
            IStore store = storeType.CreateStore(_storePath);
            var statesPath = Path.Combine(_storePath, "states");
            var genesisBlock = MineGenesisBlock();
            var stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            IStagePolicy stagePolicy = new VolatileStagePolicy();
            IBlockPolicy blockPolicy = new BlockPolicySource().GetPolicy();
            ActionEvaluator actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            BlockChain chain = BlockChain.Create(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock,
                actionEvaluator);
            var action = new HackAndSlash
            {
                Costumes = new List<Guid>(),
                Equipments = new List<Guid>(),
                Foods = new List<Guid>(),
                WorldId = 1,
                StageId = 1,
                AvatarAddress = default,
                RuneInfos = new List<RuneSlotInfo>(),
            };
            Guid chainId = chain.Id;

            for (var i = 0; i < 5; i++)
            {
                chain.MakeTransaction(GenesisHelper.ValidatorKey, new ActionBase[] { action });
                Block block = chain.ProposeBlock(
                    GenesisHelper.ValidatorKey,
                    lastCommit: GenerateBlockCommit(chain.Tip, GenesisHelper.ValidatorKey));
                chain.Append(block, GenerateBlockCommit(block, GenesisHelper.ValidatorKey));
            }

            var indexCountBeforeSnapshot = store.CountIndex(chainId);
            const int blockEpochUnitSeconds = 86400;
            var blockEpoch = (int)(chain.Tip.Timestamp.ToUnixTimeSeconds() / blockEpochUnitSeconds);
            var genesisBlockEpoch = blockEpoch - 1;
            var genesisHash = chain.Genesis.Hash;
            store.Dispose();
            stateStore.Dispose();
            const string apv = "1";
            var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            _command.Snapshot(
                apv,
                outputDirectory,
                _storePath,
                1,
                ChainCommand.SnapshotType.All);
            IStore storeAfterSnapshot = storeType.CreateStore(_storePath);
            chainId = storeAfterSnapshot.GetCanonicalChainId() ?? new Guid();
            var tipHashAfterSnapshot = storeAfterSnapshot.IndexBlockHash(chainId, -1);
            var snapshotTipIndex = storeAfterSnapshot.GetBlockIndex((BlockHash)tipHashAfterSnapshot!);
            var expectedGenesisPartitionSnapshotPath = Path.Combine(outputDirectory, "partition", $"snapshot-{genesisBlockEpoch}-{genesisBlockEpoch}.zip");
            var expectedGenesisMetadataPath = Path.Combine(outputDirectory, "metadata", $"snapshot-{genesisBlockEpoch}-{genesisBlockEpoch}.json");
            var expectedFullSnapshotPath = Path.Combine(outputDirectory, "full", $"{genesisHash}-snapshot-{tipHashAfterSnapshot}-{snapshotTipIndex}.zip");
            storeAfterSnapshot.Dispose();

            Assert.True(File.Exists(expectedGenesisPartitionSnapshotPath));
            Assert.True(File.Exists(expectedGenesisMetadataPath));
            Assert.True(File.Exists(expectedFullSnapshotPath));

            _command.Snapshot(
                apv,
                outputDirectory,
                _storePath,
                1,
                ChainCommand.SnapshotType.All);
            var expectedPartitionSnapshotPath = Path.Combine(outputDirectory, "partition", $"snapshot-{blockEpoch}-{blockEpoch}.zip");
            var expectedStateSnapshotPath = Path.Combine(outputDirectory, "state", "state_latest.zip");
            var expectedMetadataPath = Path.Combine(outputDirectory, "metadata", $"snapshot-{blockEpoch}-{blockEpoch}.json");
            storeAfterSnapshot = storeType.CreateStore(_storePath);
            chainId = storeAfterSnapshot.GetCanonicalChainId() ?? new Guid();
            var indexCountAfterSnapshot = storeAfterSnapshot.CountIndex(chainId);
            storeAfterSnapshot.Dispose();

            Assert.True(File.Exists(expectedPartitionSnapshotPath));
            Assert.True(File.Exists(expectedStateSnapshotPath));
            Assert.True(File.Exists(expectedMetadataPath));
            Assert.Equal(6, indexCountBeforeSnapshot);
            Assert.Equal(5, indexCountAfterSnapshot);

            Directory.Delete(outputDirectory, true);
        }

        private Block MineGenesisBlock()
        {
            Dictionary<string, string> tableSheets = Lib9cUtils.ImportSheets("../../../../Lib9c/Lib9c/TableCSV");
            var goldDistributionPath = Path.GetTempFileName();
            File.WriteAllText(goldDistributionPath, @"Address,AmountPerBlock,StartBlock,EndBlock
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,1000000,0,0
F9A15F870701268Bd7bBeA6502eB15F4997f32f9,100,1,100000
Fb90278C67f9b266eA309E6AE8463042f5461449,3000,3600,13600
Fb90278C67f9b266eA309E6AE8463042f5461449,100000000000,2,2
");
            var privateKey = GenesisHelper.AdminKey;
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
            Block genesisBlock = BlockHelper.ProposeGenesisBlock(
                new ValidatorSet(new List<Validator>
                {
                    new Validator(GenesisHelper.ValidatorKey.PublicKey, 10_000_000_000_000_000_000)
                }),
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

        private BlockCommit? GenerateBlockCommit(Block block, PrivateKey validator)
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
                        validator.PublicKey,
                        10_000_000_000_000_000_000,
                        VoteFlag.PreCommit).Sign(validator)))
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
