using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Types.Blocks;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Configuration;
using Nekoyume.Action.Loader;
using Nekoyume.Blockchain.Policy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Store;
using Serilog;
using Serilog.Core;
using static NineChronicles.Headless.NCActionUtils;
using CoconaUtils = Libplanet.Extensions.Cocona.Utils;

namespace NineChronicles.Headless.Executable.Commands
{
    public class ChainCommand : CoconaLiteConsoleAppBase
    {
        private readonly IConsole _console;

        public enum SnapshotType
        {
#pragma warning disable SA1602 // Enumeration items should be documented
            Full,
            Partition,
            All
#pragma warning restore SA1602 // Enumeration items should be documented
        }

        public ChainCommand(IConsole console)
        {
            _console = console;
        }

        [PrimaryCommand]
        public void Help([FromService] ICoconaHelpMessageBuilder helpMessageBuilder)
        {
            _console.Out.WriteLine(helpMessageBuilder.BuildAndRenderForCurrentContext());
        }

        [Command(Description = "Print the tip's header of the chain placed at given store path.")]
        public void Tip(
            [Argument("STORE-TYPE",
                Description = "The storage type to store blockchain data. " +
                              "You cannot use \"Memory\" because it's volatile.")]
            StoreType storeType,
            [Argument("STORE-PATH")] string storePath)
        {
            if (storeType == StoreType.Memory)
            {
                throw new CommandExitedException("Memory is volatile. " +
                                                 "Please use persistent StoreType like RocksDb.", -1);
            }

            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException($"The given STORE-PATH, {storePath} seems not existed.", -1);
            }

            IStore store = storeType.CreateStore(storePath);
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException(
                    $"There is no canonical chain: {storePath}",
                    -1);
            }

            BlockHash tipHash = store.IndexBlockHash(chainId, -1)
                          ?? throw new CommandExitedException("The given chain seems empty.", -1);
            Block tip = GetBlock(store, tipHash);
            _console.Out.WriteLine(CoconaUtils.SerializeHumanReadable(tip.Header));
            store.Dispose();
        }

        [Command(Description = "Print each block's mining time and tx stats (total tx, hack and slash, ranking battle, " +
                               "mimisbrunnr) of a given chain in csv format.")]
        public void Inspect(
            [Argument("STORE-TYPE",
                Description = "The storage type to store blockchain data. " +
                              "You cannot use \"Memory\" because it's volatile.")]
            StoreType storeType,
            [Argument("STORE-PATH",
                Description = "Store path to inspect.")]
            string storePath,
            [Argument("OFFSET",
                Description = "Offset of block index.")]
            int? offset = null,
            [Argument("LIMIT",
                Description = "Limit of block count.")]
            int? limit = null)
        {
            if (storeType == StoreType.Memory)
            {
                throw new CommandExitedException("Memory is volatile. " +
                                                 "Please use persistent StoreType like RocksDb.", -1);
            }

            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException($"The given STORE-PATH, {storePath} seems not existed.", -1);
            }

            IStagePolicy stagePolicy = new VolatileStagePolicy();
            IBlockPolicy blockPolicy = new BlockPolicySource().GetPolicy();
            IStore store = storeType.CreateStore(storePath);
            var statesPath = Path.Combine(storePath, "states");
            IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException($"There is no canonical chain: {storePath}", -1);
            }

            if (!(store.IndexBlockHash(chainId, 0) is { } gHash))
            {
                throw new CommandExitedException($"There is no genesis block: {storePath}", -1);
            }

            Block genesisBlock = GetBlock(store, gHash);
            var blockChainStates = new BlockChainStates(store, stateStore);
            var actionEvaluator = new ActionEvaluator(
                policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                stateStore,
                new NCActionLoader());
            BlockChain chain = new BlockChain(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock,
                blockChainStates,
                actionEvaluator);

            long height = chain.Tip.Index;
            if (offset + limit > (int)height)
            {
                throw new CommandExitedException(
                    $"The sum of the offset and limit is greater than the chain tip index: {height}",
                    -1);
            }

            _console.Out.WriteLine("Block Index," +
                                   "Mining Time (sec)," +
                                   "Total Tx #," +
                                   "HAS #," +
                                   "RankingBattle #," +
                                   "Mimisbrunnr #");

            var typeOfActionTypeAttribute = typeof(ActionTypeAttribute);
            foreach (var item in
                store.IterateIndexes(chain.Id, offset + 1 ?? 1, limit).Select((value, i) => new { i, value }))
            {
                var block = GetBlock(store, item.value);
                var previousBlock = GetBlock(store,
                    block.PreviousHash ?? block.Hash
                );

                var miningTime = block.Timestamp - previousBlock.Timestamp;
                var txCount = 0;
                var hackAndSlashCount = 0;
                var rankingBattleCount = 0;
                var mimisbrunnrBattleCount = 0;
                foreach (var tx in block.Transactions)
                {
                    txCount++;
                    foreach (var action in tx.Actions!)
                    {
                        var actionTypeAttribute =
                            Attribute.GetCustomAttribute(ToAction(action).GetType(), typeOfActionTypeAttribute)
                                as ActionTypeAttribute;
                        if (actionTypeAttribute is null)
                        {
                            continue;
                        }

                        var typeIdentifier = actionTypeAttribute.TypeIdentifier;
                        if (typeIdentifier is Text text)
                        {
                            var typeIdentifierStr = text.Value;
                            if (typeIdentifierStr.StartsWith("hack_and_slash"))
                            {
                                hackAndSlashCount++;
                            }
                            else if (typeIdentifierStr.StartsWith("ranking_battle"))
                            {
                                rankingBattleCount++;
                            }
                            else if (typeIdentifierStr.StartsWith("mimisbrunnr_battle"))
                            {
                                mimisbrunnrBattleCount++;
                            }
                        }
                    }
                }

                _console.Out.WriteLine($"{block.Index}," +
                                       $"{miningTime:s\\.ff}," +
                                       $"{txCount}," +
                                       $"{hackAndSlashCount}," +
                                       $"{rankingBattleCount}," +
                                       $"{mimisbrunnrBattleCount}");
            }

            store.Dispose();
            stateStore.Dispose();
        }

        [Command(Description = "Truncate the chain from the tip by the input value (in blocks)")]
        public void Truncate(
            [Argument("STORE-TYPE",
                Description = "The storage type to store blockchain data. " +
                              "You cannot use \"Memory\" because it's volatile.")]
            StoreType storeType,
            [Argument("STORE-PATH",
                Description = "Store path to inspect.")]
            string storePath,
            [Argument("BLOCKS-BEFORE",
                Description = "Number of blocks to truncate from the tip")]
            int blocksBefore)
        {
            if (storeType == StoreType.Memory)
            {
                throw new CommandExitedException("Memory is volatile. " +
                                                 "Please use persistent StoreType like RocksDb.", -1);
            }

            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException(
                    $"The given STORE-PATH, {storePath} seems not existed.",
                    -1);
            }

            IStore store = storeType.CreateStore(storePath);
            var statesPath = Path.Combine(storePath, "states");
            IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException(
                    $"There is no canonical chain: {storePath}",
                    -1);
            }

            if (!(store.IndexBlockHash(chainId, 0) is { }))
            {
                throw new CommandExitedException(
                    $"There is no genesis block: {storePath}",
                    -1);
            }

            var tipHash = store.IndexBlockHash(chainId, -1)
                          ?? throw new CommandExitedException("The given chain seems empty.", -1);
            if (!(store.GetBlockIndex(tipHash) is { } tipIndex))
            {
                throw new CommandExitedException(
                    $"The index of {tipHash} doesn't exist.",
                    -1);
            }

            var tip = GetBlock(store, tipHash);
            var snapshotTipIndex = Math.Max(tipIndex - (blocksBefore + 1), 0);
            BlockHash snapshotTipHash;

            do
            {
                snapshotTipIndex++;
                _console.Out.WriteLine(snapshotTipIndex);
                if (!(store.IndexBlockHash(chainId, snapshotTipIndex) is { } hash))
                {
                    throw new CommandExitedException(
                        $"The index {snapshotTipIndex} doesn't exist on ${chainId}.",
                        -1);
                }

                snapshotTipHash = hash;
            } while (!stateStore.GetStateRoot(GetBlock(store, snapshotTipHash).StateRootHash).Recorded);

            var forkedId = Guid.NewGuid();

            Fork(chainId, forkedId, snapshotTipHash, tip, store);

            store.SetCanonicalChainId(forkedId);
            foreach (var id in store.ListChainIds().Where(id => !id.Equals(forkedId)))
            {
                store.DeleteChainId(id);
            }

            store.Dispose();
            stateStore.Dispose();
        }

        [Command(Description = "Prune states in the chain")]
        public void PruneStates(
            [Argument("STORE-TYPE",
                Description = "Store type of RocksDb.")]
            StoreType storeType,
            [Argument("STORE-PATH",
                Description = "Store path to prune states.")]
            string storePath)
        {
            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException(
                    $"The given STORE-PATH, {storePath} seems not existed.",
                    -1);
            }

            IStore store = storeType.CreateStore(storePath);
            var statesPath = Path.Combine(storePath, "states");
            IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException(
                    $"There is no canonical chain: {storePath}",
                    -1);
            }

            if (!(store.IndexBlockHash(chainId, 0) is { }))
            {
                throw new CommandExitedException(
                    $"There is no genesis block: {storePath}",
                    -1);
            }

            var tipHash = store.IndexBlockHash(chainId, -1)
                          ?? throw new CommandExitedException("The given chain seems empty.", -1);

            if (!(store.GetBlockIndex(tipHash) is { }))
            {
                throw new CommandExitedException(
                    $"The index of {tipHash} doesn't exist.",
                    -1);
            }

            var newStatesPath = Path.Combine(storePath, "new_states");
            IKeyValueStore newStateKeyValueStore = new RocksDBKeyValueStore(newStatesPath);
            var newStateStore = new TrieStateStore(newStateKeyValueStore);
            if (!(store.GetStateRootHash(tipHash) is { } snapshotTipStateRootHash))
            {
                throw new CommandExitedException(
                    $"The StateRootHash of {tipHash} doesn't exist.",
                    -1);
            }

            _console.Out.WriteLine($"Pruning States Start.");
            var start = DateTimeOffset.Now;
            stateStore.CopyStates(ImmutableHashSet<HashDigest<SHA256>>.Empty
                .Add(snapshotTipStateRootHash), newStateStore);
            var end = DateTimeOffset.Now;
            _console.Out.WriteLine($"Pruning States Done.Time Taken: {end - start:g}");
            store.Dispose();
            stateStore.Dispose();
            newStateStore.Dispose();
            Directory.Delete(statesPath, true);
            Directory.Move(newStatesPath, statesPath);
        }

        [Command(Description = "Take a chain snapshot and store it in a designated directory")]
        public void Snapshot(
            [Argument("APV",
                Description = "APV to include in the snapshot metadata")]
            string apv,
            [Argument("OUTPUT-DIRECTORY",
                Description = "Directory path to store the snapshot file")]
            string outputDirectory,
            [Argument("STORE-PATH",
                Description = "Store path of the chain to take a snapshot")]
            string? storePath = null,
            [Argument("BLOCK-BEFORE",
                Description = "Number of blocks to truncate from the tip")]
            int blockBefore = 1,
            [Argument("SNAPSHOT-TYPE",
                Description = "Type of snapshot to take (full, partition, or all)")]
            SnapshotType snapshotType = SnapshotType.Partition)
        {
            try
            {
                var snapshotStart = DateTimeOffset.Now;
                _console.Out.WriteLine($"Create Snapshot-{snapshotType.ToString()} start.");

                // If store changed epoch unit seconds, this will be changed too
                const int epochUnitSeconds = 86400;
                string defaultStorePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "planetarium",
                    "9c"
                );

                if (blockBefore < 0)
                {
                    throw new CommandExitedException("The --block-before option must be greater than or equal to 0.",
                        -1);
                }

                Directory.CreateDirectory(outputDirectory);
                Directory.CreateDirectory(Path.Combine(outputDirectory, "partition"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "state"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "metadata"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "full"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "temp"));

                outputDirectory = string.IsNullOrEmpty(outputDirectory)
                    ? Environment.CurrentDirectory
                    : outputDirectory;

                var metadataDirectory = Path.Combine(outputDirectory, "metadata");
                var tempDirectory = Path.Combine(outputDirectory, "temp");
                int currentMetadataBlockEpoch = GetMetaDataEpoch(metadataDirectory, "BlockEpoch");
                int currentMetadataTxEpoch = GetMetaDataEpoch(metadataDirectory, "TxEpoch");
                int previousMetadataBlockEpoch = GetMetaDataEpoch(metadataDirectory, "PreviousBlockEpoch");

                storePath = string.IsNullOrEmpty(storePath) ? defaultStorePath : storePath;
                if (!Directory.Exists(storePath))
                {
                    throw new CommandExitedException("Invalid store path. Please check --store-path is valid.", -1);
                }

                var statesPath = Path.Combine(storePath, "states");
                var mainPath = Path.Combine(storePath, "9c-main");
                var stateRefPath = Path.Combine(storePath, "stateref");
                var statePath = Path.Combine(storePath, "state");
                var newStatesPath = Path.Combine(storePath, "new_states");
                var stateHashesPath = Path.Combine(storePath, "state_hashes");

                var staleDirectories =
                    new[] { mainPath, statePath, stateRefPath, stateHashesPath };
                staleDirectories.Where(Directory.Exists).ToList()
                    .ForEach(staleDirectory => Directory.Delete(staleDirectory, true));


                if (RocksDBStore.MigrateChainDBFromColumnFamilies(Path.Combine(storePath, "chain")))
                {
                    _console.Out.WriteLine("Successfully migrated IndexDB.");
                }
                else
                {
                    _console.Out.WriteLine("Migration not required.");
                }

                RocksDBStore store = new RocksDBStore(storePath);
                IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
                IKeyValueStore newStateKeyValueStore = new RocksDBKeyValueStore(newStatesPath);
                TrieStateStore stateStore = new TrieStateStore(stateKeyValueStore);
                TrieStateStore newStateStore = new TrieStateStore(newStateKeyValueStore);

                var canonicalChainId = store.GetCanonicalChainId();
                if (!(canonicalChainId is { } chainId))
                {
                    throw new CommandExitedException("Canonical chain doesn't exist.", -1);
                }

                var genesisHash = store.IterateIndexes(chainId, 0, 1).First();
                var tipHash = store.IndexBlockHash(chainId, -1)
                              ?? throw new CommandExitedException("The given chain seems empty.", -1);
                if (!(store.GetBlockIndex(tipHash) is { } tipIndex))
                {
                    throw new CommandExitedException(
                        $"The index of {tipHash} doesn't exist.",
                        -1);
                }

                IStagePolicy stagePolicy = new VolatileStagePolicy();
                IBlockPolicy blockPolicy =
                    new BlockPolicy();
                var blockChainStates = new BlockChainStates(store, stateStore);
                var actionEvaluator = new ActionEvaluator(
                    policyActionsRegistry: blockPolicy.PolicyActionsRegistry,
                    stateStore,
                    new NCActionLoader()
                );
                var originalChain = new BlockChain(blockPolicy, stagePolicy, store, stateStore,
                    store.GetBlock(genesisHash), blockChainStates, actionEvaluator);
                var tip = GetBlock(store, tipHash);

                var potentialSnapshotTipIndex = tipIndex - blockBefore;
                var potentialSnapshotTipHash = (BlockHash)store.IndexBlockHash(chainId, potentialSnapshotTipIndex)!;
                var snapshotTip = GetBlock(store, potentialSnapshotTipHash);

                _console.Out.WriteLine(
                    "Original Store Tip: #{0}\n1. LastCommit: {1}\n2. BlockCommit in Chain: {2}\n3. BlockCommit in Store: {3}",
                    tip.Index, tip.LastCommit, originalChain.GetBlockCommit(tipHash), store.GetBlockCommit(tipHash));
                _console.Out.WriteLine(
                    "Potential Snapshot Tip: #{0}\n1. LastCommit: {1}\n2. BlockCommit in Chain: {2}\n3. BlockCommit in Store: {3}",
                    potentialSnapshotTipIndex, snapshotTip.LastCommit,
                    originalChain.GetBlockCommit(potentialSnapshotTipHash),
                    store.GetBlockCommit(potentialSnapshotTipHash));

                var tipBlockCommit = store.GetBlockCommit(tipHash) ??
                                     originalChain.GetBlockCommit(tipHash);
                var potentialSnapshotTipBlockCommit = store.GetBlockCommit(potentialSnapshotTipHash) ??
                                                      originalChain.GetBlockCommit(potentialSnapshotTipHash);

                // Add tip and the snapshot tip's block commit to store to avoid block validation during preloading
                if (potentialSnapshotTipBlockCommit != null)
                {
                    _console.Out.WriteLine("Adding the tip(#{0}) and the snapshot tip(#{1})'s block commit to the store",
                        tipIndex, snapshotTip.Index);
                    store.PutBlockCommit(tipBlockCommit);
                    store.PutChainBlockCommit(chainId, tipBlockCommit);
                    store.PutBlockCommit(potentialSnapshotTipBlockCommit);
                    store.PutChainBlockCommit(chainId, potentialSnapshotTipBlockCommit);
                }
                else
                {
                    _console.Out.WriteLine(
                        "There is no block commit associated with the potential snapshot tip: #{0}. Snapshot will automatically truncate 1 more block from the original chain tip.",
                        potentialSnapshotTipIndex);
                    blockBefore += 1;
                    potentialSnapshotTipBlockCommit =
                        GetBlock(store, (BlockHash)store.IndexBlockHash(chainId, tip.Index - blockBefore + 1)!).LastCommit;
                    if (potentialSnapshotTipBlockCommit == null)
                    {
                        throw new CommandExitedException(
                            $"The block commit of the potential snapshot tip: #{potentialSnapshotTipIndex} doesn't exist.",
                            -1);
                    }

                    store.PutBlockCommit(tipBlockCommit);
                    store.PutChainBlockCommit(chainId, tipBlockCommit);
                    store.PutBlockCommit(potentialSnapshotTipBlockCommit);
                    store.PutChainBlockCommit(chainId, potentialSnapshotTipBlockCommit);
                }

                var blockCommitBlock = GetBlock(store, tipHash);

                // Add last block commits to store from tip until --block-before + 5 or tip if too short for buffer
                var blockCommitRange = blockBefore + 5 < tip.Index ? blockBefore + 5 : tip.Index - 1;
                for (var i = 0; i < blockCommitRange; i++)
                {
                    _console.Out.WriteLine("Adding block #{0}'s block commit to the store", blockCommitBlock.Index - 1);
                    if (blockCommitBlock.LastCommit == null)
                    {
                        throw new CommandExitedException(
                            $"The block commit of the block: #{blockCommitBlock.Index} doesn't exist.",
                            -1);
                    }

                    store.PutBlockCommit(blockCommitBlock.LastCommit);
                    store.PutChainBlockCommit(chainId, blockCommitBlock.LastCommit);
                    blockCommitBlock = GetBlock(store, (BlockHash)blockCommitBlock.PreviousHash!);
                }

                var snapshotTipIndex = Math.Max(tipIndex - (blockBefore + 1), 0);
                BlockHash snapshotTipHash;

                do
                {
                    snapshotTipIndex++;

                    if (!(store.IndexBlockHash(chainId, snapshotTipIndex) is { } hash))
                    {
                        throw new CommandExitedException(
                            $"The index {snapshotTipIndex} doesn't exist on ${chainId}.",
                            -1);
                    }

                    snapshotTipHash = hash;
                } while (!stateStore.GetStateRoot(GetBlock(store, snapshotTipHash).StateRootHash).Recorded);

                var forkedId = Guid.NewGuid();
                Fork(chainId, forkedId, snapshotTipHash, tip, store);

                store.SetCanonicalChainId(forkedId);
                foreach (var id in store.ListChainIds().Where(id => !id.Equals(forkedId)))
                {
                    store.DeleteChainId(id);
                }

                var snapshotTipDigest = store.GetBlockDigest(snapshotTipHash);
                var snapshotTipStateRootHash = store.GetStateRootHash(snapshotTipHash);
                ImmutableHashSet<HashDigest<SHA256>> stateHashes =
                    ImmutableHashSet<HashDigest<SHA256>>.Empty.Add((HashDigest<SHA256>)snapshotTipStateRootHash!);

                // Get 1 block digest before snapshot tip using snapshot previous block hash.
                BlockHash? previousBlockHash = snapshotTipDigest?.Hash;
                int count = 0;
                const int maxStateDepth = 1;

                while (previousBlockHash is { } pbh &&
                       store.GetBlockDigest(pbh) is { } previousBlockDigest &&
                       count < maxStateDepth)
                {
                    stateHashes = stateHashes.Add(previousBlockDigest.StateRootHash);
                    previousBlockHash = previousBlockDigest.PreviousHash;
                    count++;
                }

                var newChain = new BlockChain(blockPolicy, stagePolicy, store, stateStore,
                    store.GetBlock(genesisHash), blockChainStates, actionEvaluator);
                var newTip = newChain.Tip;
                var latestEpoch = (int)(newTip.Timestamp.ToUnixTimeSeconds() / epochUnitSeconds);
                _console.Out.WriteLine(
                    "Official Snapshot Tip: #{0}\n1. Timestamp: {1}\n2. Latest Epoch: {2}\n3. BlockCommit in Chain: {3}\n4. BlockCommit in Store: {4}",
                    newTip.Index, newTip.Timestamp.UtcDateTime, latestEpoch, newChain.GetBlockCommit(newTip.Hash),
                    store.GetBlockCommit(newTip.Hash));

                _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} CopyStates Start.");
                var start = DateTimeOffset.Now;
                stateStore.CopyStates(stateHashes, newStateStore);
                _console.Out.WriteLine(
                    $"Snapshot-{snapshotType.ToString()} CopyStates Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");

                store.Dispose();
                stateStore.Dispose();
                stateKeyValueStore.Dispose();
                newStateStore.Dispose();
                newStateKeyValueStore.Dispose();

                _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Move States Start.");
                start = DateTimeOffset.Now;
                Directory.Delete(statesPath, recursive: true);
                Directory.Move(newStatesPath, statesPath);
                _console.Out.WriteLine(
                    $"Snapshot-{snapshotType.ToString()} Move States Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min");

                var partitionBaseFilename = GetPartitionBaseFileName(
                    currentMetadataBlockEpoch,
                    currentMetadataTxEpoch,
                    latestEpoch);
                var stateBaseFilename = "state_latest";

                var fullSnapshotDirectory = Path.Combine(outputDirectory, "full");
                var genesisHashHex = ByteUtil.Hex(genesisHash.ToByteArray());
                var snapshotTipHashHex = ByteUtil.Hex(snapshotTipHash.ToByteArray());
                var fullSnapshotFilename = $"{genesisHashHex}-snapshot-{snapshotTipHashHex}-{snapshotTipIndex}.zip";
                var fullSnapshotPath = Path.Combine(fullSnapshotDirectory, fullSnapshotFilename);

                var partitionSnapshotFilename = $"{partitionBaseFilename}.zip";
                var partitionSnapshotPath = Path.Combine(outputDirectory, "partition", partitionSnapshotFilename);
                var stateSnapshotFilename = $"{stateBaseFilename}.zip";
                var stateSnapshotPath = Path.Combine(outputDirectory, "state", stateSnapshotFilename);
                string partitionDirectory = Path.Combine(tempDirectory, "snapshot");
                string stateDirectory = Path.Combine(tempDirectory, "state");

                if (Directory.Exists(partitionDirectory))
                {
                    Directory.Delete(partitionDirectory, true);
                }

                if (Directory.Exists(stateDirectory))
                {
                    Directory.Delete(stateDirectory, true);
                }

                _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Clean Store Start.");
                start = DateTimeOffset.Now;
                CleanStore(
                    partitionSnapshotPath,
                    stateSnapshotPath,
                    fullSnapshotPath,
                    storePath);
                _console.Out.WriteLine(
                    $"Snapshot-{snapshotType.ToString()} Clean Store Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");

                if (snapshotType == SnapshotType.Partition || snapshotType == SnapshotType.All)
                {
                    var storeBlockPath = Path.Combine(storePath, "block");
                    var storeTxPath = Path.Combine(storePath, "tx");
                    var partitionDirBlockPath = Path.Combine(partitionDirectory, "block");
                    var partitionDirTxPath = Path.Combine(partitionDirectory, "tx");
                    _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Clone Partition Directory Start.");
                    start = DateTimeOffset.Now;
                    CopyDirectory(storeBlockPath, partitionDirBlockPath, true);
                    CopyDirectory(storeTxPath, partitionDirTxPath, true);
                    _console.Out.WriteLine(
                        $"Snapshot-{snapshotType.ToString()} Clone Partition Directory Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");

                    // get epoch limit for block & tx
                    var epochLimit = GetEpochLimit(
                        latestEpoch,
                        currentMetadataBlockEpoch,
                        previousMetadataBlockEpoch);
                    _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Clean Partition Store Start.");

                    // clean epoch directories in block & tx
                    start = DateTimeOffset.Now;
                    CleanEpoch(partitionDirBlockPath, epochLimit);
                    CleanEpoch(partitionDirTxPath, epochLimit);
                    CleanPartitionStore(partitionDirectory);
                    _console.Out.WriteLine(
                        $"Snapshot-{snapshotType.ToString()} Clean Partition Store Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");

                    _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Clone State Directory Start.");
                    start = DateTimeOffset.Now;
                    CopyStateStore(storePath, stateDirectory);
                    _console.Out.WriteLine(
                        $"Snapshot-{snapshotType.ToString()} Clone State Directory Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");
                }

                if (snapshotType == SnapshotType.Full || snapshotType == SnapshotType.All)
                {
                    _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Create Full ZipFile Start.");
                    start = DateTimeOffset.Now;
                    ZipFile.CreateFromDirectory(storePath, fullSnapshotPath);
                    _console.Out.WriteLine(
                        $"Snapshot-{snapshotType.ToString()} Create Full ZipFile Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");
                }

                if (snapshotType == SnapshotType.Partition || snapshotType == SnapshotType.All)
                {
                    _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Create Partition ZipFile Start.");
                    start = DateTimeOffset.Now;
                    ZipFile.CreateFromDirectory(partitionDirectory, partitionSnapshotPath);
                    _console.Out.WriteLine(
                        $"Snapshot-{snapshotType.ToString()} Create Partition ZipFile Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");

                    _console.Out.WriteLine($"Snapshot-{snapshotType.ToString()} Create State ZipFile Start.");
                    start = DateTimeOffset.Now;
                    ZipFile.CreateFromDirectory(stateDirectory, stateSnapshotPath);
                    _console.Out.WriteLine(
                        $"Snapshot-{snapshotType.ToString()} Create State ZipFile Done. Time Taken: {(DateTimeOffset.Now - start).TotalMinutes} min.");

                    if (snapshotTipDigest is null)
                    {
                        throw new CommandExitedException("Tip does not exist.", -1);
                    }

                    string stringifyMetadata = CreateMetadata(
                        snapshotTipDigest.Value,
                        apv,
                        currentMetadataBlockEpoch,
                        currentMetadataTxEpoch,
                        previousMetadataBlockEpoch,
                        latestEpoch);
                    var metadataFilename = $"{partitionBaseFilename}.json";
                    var metadataPath = Path.Combine(metadataDirectory, metadataFilename);

                    if (File.Exists(metadataPath))
                    {
                        File.Delete(metadataPath);
                    }

                    File.WriteAllText(metadataPath, stringifyMetadata);
                    Directory.Delete(partitionDirectory, true);
                    Directory.Delete(stateDirectory, true);
                }

                _console.Out.WriteLine(
                    $"Create Snapshot-{snapshotType.ToString()} Complete. Time Taken: {(DateTimeOffset.Now - snapshotStart).TotalMinutes} min.");
            }
            catch (Exception ex)
            {
                _console.Error.WriteLine(ex.Message);
                _console.Error.WriteLine(ex.StackTrace);
            }
        }

        private static Block GetBlock(IStore store, BlockHash blockHash)
        {
            return store.GetBlock(blockHash) ??
                throw new CommandExitedException($"The block of {blockHash} doesn't exist.", -1);
        }

        private static BlockCommit GetBlockCommit(IStore store, BlockHash blockHash)
        {
            return store.GetBlockCommit(blockHash) ??
                throw new CommandExitedException($"The block commit of {blockHash} doesn't exist.", -1);
        }

        private string GetPartitionBaseFileName(
            int currentMetadataBlockEpoch,
            int currentMetadataTxEpoch,
            int latestBlockEpoch
        )
        {
            // decrease latest epochs by 1 when creating genesis snapshot
            if (currentMetadataBlockEpoch == 0 && currentMetadataTxEpoch == 0)
            {
                return $"snapshot-{latestBlockEpoch - 1}-{latestBlockEpoch - 1}";
            }
            else
            {
                return $"snapshot-{latestBlockEpoch}-{latestBlockEpoch}";
            }
        }

        private int GetEpochLimit(
            int latestEpoch,
            int currentMetadataEpoch,
            int previousMetadataEpoch
        )
        {
            if (latestEpoch == currentMetadataEpoch)
            {
                // case when all epochs are the same
                if (latestEpoch == previousMetadataEpoch)
                {
                    // return previousMetadataEpoch - 1
                    // to save previous epoch in snapshot
                    return previousMetadataEpoch - 1;
                }

                // case when metadata points to genesis snapshot
                if (previousMetadataEpoch == 0)
                {
                    return currentMetadataEpoch - 1;
                }

                return previousMetadataEpoch;
            }

            return currentMetadataEpoch;
        }

        private string CreateMetadata(
            BlockDigest snapshotTipDigest,
            string apv,
            int currentMetadataBlockEpoch,
            int currentMetadataTxEpoch,
            int previousMetadataBlockEpoch,
            int latestBlockEpoch)
        {
            BlockHeader snapshotTipHeader = snapshotTipDigest.GetHeader();
            JObject jsonObject = JObject.FromObject(snapshotTipHeader);
            jsonObject.Add("APV", apv);

            jsonObject = AddPreviousEpochs(
                jsonObject,
                currentMetadataBlockEpoch,
                previousMetadataBlockEpoch,
                latestBlockEpoch,
                "PreviousBlockEpoch",
                "PreviousTxEpoch");

            // decrease latest epochs by 1 for genesis snapshot
            if (currentMetadataBlockEpoch == 0 && currentMetadataTxEpoch == 0)
            {
                jsonObject.Add("BlockEpoch", latestBlockEpoch - 1);
                jsonObject.Add("TxEpoch", latestBlockEpoch - 1);
            }
            else
            {
                jsonObject.Add("BlockEpoch", latestBlockEpoch);
                jsonObject.Add("TxEpoch", latestBlockEpoch);
            }

            return JsonConvert.SerializeObject(jsonObject);
        }

        private void CleanStore(
            string partitionSnapshotPath,
            string stateSnapshotPath,
            string fullSnapshotPath,
            string storePath)
        {
            if (File.Exists(partitionSnapshotPath))
            {
                File.Delete(partitionSnapshotPath);
            }

            if (File.Exists(stateSnapshotPath))
            {
                File.Delete(stateSnapshotPath);
            }

            if (File.Exists(fullSnapshotPath))
            {
                File.Delete(fullSnapshotPath);
            }

            var cleanDirectories = new[]
            {
                Path.Combine(storePath, "blockpercept"),
                Path.Combine(storePath, "stagedtx")
            };

#pragma warning disable S3267
            foreach (var path in cleanDirectories)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
#pragma warning restore S3267
        }

        private void CleanPartitionStore(string partitionDirectory)
        {
            var cleanDirectories = new[]
            {
                Path.Combine(partitionDirectory, "block", "blockindex"),
                Path.Combine(partitionDirectory, "tx", "txindex"),
            };

#pragma warning disable S3267
            foreach (var path in cleanDirectories)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
#pragma warning restore S3267
        }

        private void CopyStateStore(string storePath, string stateDirectory)
        {
            var storeBlockIndexPath = Path.Combine(storePath, "block", "blockindex");
            var storeTxIndexPath = Path.Combine(storePath, "tx", "txindex");
            var storeTxBIndexPath = Path.Combine(storePath, "txbindex");
            var storeStatesPath = Path.Combine(storePath, "states");
            var storeChainPath = Path.Combine(storePath, "chain");
            var storeBlockCommitPath = Path.Combine(storePath, "blockcommit");
            var storeTxExecPath = Path.Combine(storePath, "txexec");
            var stateDirBlockIndexPath = Path.Combine(stateDirectory, "block", "blockindex");
            var stateDirTxIndexPath = Path.Combine(stateDirectory, "tx", "txindex");
            var stateDirTxBIndexPath = Path.Combine(stateDirectory, "txbindex");
            var stateDirStatesPath = Path.Combine(stateDirectory, "states");
            var stateDirChainPath = Path.Combine(stateDirectory, "chain");
            var stateDirBlockCommitPath = Path.Combine(stateDirectory, "blockcommit");
            var stateDirTxExecPath = Path.Combine(stateDirectory, "txexec");
            CopyDirectory(storeBlockIndexPath, stateDirBlockIndexPath, true);
            CopyDirectory(storeTxIndexPath, stateDirTxIndexPath, true);
            CopyDirectory(storeTxBIndexPath, stateDirTxBIndexPath, true);
            CopyDirectory(storeStatesPath, stateDirStatesPath, true);
            CopyDirectory(storeChainPath, stateDirChainPath, true);
            CopyDirectory(storeBlockCommitPath, stateDirBlockCommitPath, true);
            CopyDirectory(storeTxExecPath, stateDirTxExecPath, true);
        }

        private int GetMetaDataEpoch(
            string outputDirectory,
            string epochType)
        {
            try
            {
                string previousMetadata = Directory.GetFiles(outputDirectory)
                    .Where(x => Path.GetExtension(x) == ".json")
                    .OrderByDescending(File.GetLastWriteTime)
                    .First();
                var jsonObject = JObject.Parse(File.ReadAllText(previousMetadata));
                return (int)jsonObject[epochType]!;
            }
            catch (InvalidOperationException e)
            {
                _console.Error.WriteLine(e.Message);
                return 0;
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            try
            {
                // Get information about the source directory
                var dir = new DirectoryInfo(sourceDir);

                // Check if the source directory exists
                if (!dir.Exists)
                {
                    throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
                }

                // Cache directories before we start copying
                DirectoryInfo[] dirs = dir.GetDirectories();

                // Create the destination directory
                Directory.CreateDirectory(destinationDir);

                // Get the files in the source directory and copy to the destination directory
                foreach (FileInfo file in dir.GetFiles())
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath);
                }

                // If recursive and copying subdirectories, recursively call this method
                if (recursive)
                {
                    foreach (DirectoryInfo subDir in dirs)
                    {
                        string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                        CopyDirectory(subDir.FullName, newDestinationDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _console.Out.WriteLine(ex.Message);
            }
        }

        private void CleanEpoch(string path, int epochLimit)
        {
            string[] directories = Directory.GetDirectories(
                path,
                "epoch*",
                SearchOption.AllDirectories);
            try
            {
                foreach (string dir in directories)
                {
                    string dirName = new DirectoryInfo(dir).Name;
                    int epoch = int.Parse(dirName.Substring(5));
                    if (epoch < epochLimit)
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            catch (FormatException)
            {
                throw new FormatException("Epoch value is not numeric.");
            }
        }

        private JObject AddPreviousEpochs(
            JObject jsonObject,
            int currentMetadataEpoch,
            int previousMetadataEpoch,
            int latestEpoch,
            string blockEpochName,
            string txEpochName)
        {
            if (currentMetadataEpoch == latestEpoch)
            {
                jsonObject.Add(blockEpochName, previousMetadataEpoch);
                jsonObject.Add(txEpochName, previousMetadataEpoch);
            }
            else
            {
                jsonObject.Add(blockEpochName, currentMetadataEpoch);
                jsonObject.Add(txEpochName, currentMetadataEpoch);
            }

            return jsonObject;
        }

        private void Fork(
            Guid src,
            Guid dest,
            BlockHash branchPointHash,
            Block tip,
            IStore store)
        {
            store.ForkBlockIndexes(src, dest, branchPointHash);
            if (store.GetBlockCommit(branchPointHash) is { })
            {
                store.PutChainBlockCommit(dest, GetBlockCommit(store, branchPointHash));
            }
            store.ForkTxNonces(src, dest);

            for (
                Block block = tip;
                block.PreviousHash is { } hash
                && !block.Hash.Equals(branchPointHash);
                block = GetBlock(store, hash))
            {
                IEnumerable<(Address, int)> signers = block
                    .Transactions
                    .GroupBy(tx => tx.Signer)
                    .Select(g => (g.Key, g.Count()));

                foreach ((Address address, int txCount) in signers)
                {
                    store.IncreaseTxNonce(dest, address, -txCount);
                }
            }
        }
    }
}
