using System;
using System.IO;
using System.Linq;
using Cocona;
using Cocona.Help;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Extensions.Cocona;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.Action;
using Nekoyume.BlockChain.Policy;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Store;
using Serilog.Core;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable.Commands
{
    public class ChainCommand : CoconaLiteConsoleAppBase
    {
        private readonly IConsole _console;

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
            [Argument("STORE-TYPE")]
            StoreType storeType,
            [Argument("STORE-PATH")]
            string storePath)
        {
            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException($"The given STORE-PATH, {storePath} seems not existed.", -1);
            }

            const int minimumDifficulty = 5000000, maximumTransactions = 100;
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy(minimumDifficulty, maximumTransactions);
            IStore store = storeType.CreateStore(storePath);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            Block<NCAction> genesisBlock = store.GetGenesisBlock<NCAction>(blockPolicy.GetHashAlgorithm);
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);
            _console.Out.WriteLine(Utils.SerializeHumanReadable(chain.Tip.Header));
            (store as IDisposable)?.Dispose();
        }

        [Command(Description = "Print each block's mining time and tx stats (total tx, hack and slash, ranking battle, " +
                               "mimisbrunnr) of a given chain in csv format.")]
        public void Inspect(
            [Argument("STORE-TYPE",
                Description = "Store type of RocksDb (rocksdb or monorocksdb).")]
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
            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException($"The given STORE-PATH, {storePath} seems not existed.", -1);
            }

            const int minimumDifficulty = 5000000, maximumTransactions = 100;
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy(minimumDifficulty, maximumTransactions);
            IStore store = storeType.CreateStore(storePath);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            Block<NCAction> genesisBlock = store.GetGenesisBlock<NCAction>(blockPolicy.GetHashAlgorithm);
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException($"There is no canonical chain: {storePath}", -1);
            }

            if (!(store.IndexBlockHash(chainId, 0) is { } gHash))
            {
                throw new CommandExitedException($"There is no genesis block: {storePath}", -1);
            }

            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);

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
            foreach (var item in
                store.IterateIndexes(chain.Id, offset + 1 ?? 1, limit).Select((value, i) => new { i, value }))
            {
                var block = store.GetBlock<NCAction>(blockPolicy.GetHashAlgorithm, item.value);
                var previousBlock = store.GetBlock<NCAction>(
                    blockPolicy.GetHashAlgorithm,
                    block.PreviousHash ?? block.Hash
                );

                var miningTime = block.Timestamp - previousBlock.Timestamp;
                var txCount = 0;
                var hackandslashCount = 0;
                var rankingbattleCount = 0;
                var mimisbrunnrCount = 0;
                foreach (var tx in block.Transactions)
                {
                    txCount++;
                    foreach (var action in tx.Actions)
                    {
                        switch (action.InnerAction)
                        {
                            case HackAndSlash _:
                            case HackAndSlash0 _:
                            case HackAndSlash2 _:
                            case HackAndSlash3 _:
                            case HackAndSlash4 _:
                            case HackAndSlash5 _:
                            case HackAndSlash6 _:
                                hackandslashCount++;
                                break;
                            case MimisbrunnrBattle  _:
                            case MimisbrunnrBattle0  _:
                            case MimisbrunnrBattle2  _:
                            case MimisbrunnrBattle3  _:
                            case MimisbrunnrBattle4  _:
                                mimisbrunnrCount++;
                                break;
                            case RankingBattle _:
                            case RankingBattle0 _:
                            case RankingBattle2 _:
                            case RankingBattle3 _:
                            case RankingBattle4 _:
                            case RankingBattle5 _:
                                rankingbattleCount++;
                                break;
                        }
                    }
                }

                _console.Out.WriteLine($"{block.Index}," +
                                       $"{miningTime:s\\.ff}," +
                                       $"{txCount}," +
                                       $"{hackandslashCount}," +
                                       $"{rankingbattleCount}," +
                                       $"{mimisbrunnrCount}");
            }

            (store as IDisposable)?.Dispose();
        }
    }
}
