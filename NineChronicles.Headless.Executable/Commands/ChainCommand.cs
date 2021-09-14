using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cocona;
using Cocona.Help;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Store;
using Nekoyume.Action;
using Nekoyume.BlockChain;
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
            Block<NCAction> genesisBlock = store.GetGenesisBlock<NCAction>();
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                new NoOpStateStore(),
                genesisBlock);
            _console.Out.WriteLine(JsonSerializer.Serialize(chain.Tip.Header));
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
            Block<NCAction> genesisBlock = store.GetGenesisBlock<NCAction>();
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
                new NoOpStateStore(),
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
                var block = store.GetBlock<NCAction>(item.value);

                var previousBlock = store.GetBlock<NCAction>(block.PreviousHash ?? block.Hash);

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
                        if (action.InnerAction is HackAndSlash hackandslashAction)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash7 hackandslashAction7)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash6 hackandslashAction6)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash5 hackandslashAction5)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash4 hackandslashAction4)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash3 hackandslashAction3)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash2 hackandslashAction2)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is HackAndSlash0 hackandslashAction0)
                        {
                            hackandslashCount++;
                        }

                        if (action.InnerAction is MimisbrunnrBattle mimisbrunnrAction)
                        {
                            mimisbrunnrCount++;
                        }

                        if (action.InnerAction is MimisbrunnrBattle4 mimisbrunnrAction4)
                        {
                            mimisbrunnrCount++;
                        }

                        if (action.InnerAction is MimisbrunnrBattle3 mimisbrunnrAction3)
                        {
                            mimisbrunnrCount++;
                        }

                        if (action.InnerAction is MimisbrunnrBattle2 mimisbrunnrAction2)
                        {
                            mimisbrunnrCount++;
                        }

                        if (action.InnerAction is MimisbrunnrBattle0 mimisbrunnrAction0)
                        {
                            mimisbrunnrCount++;
                        }

                        if (action.InnerAction is RankingBattle rankingbattleAction)
                        {
                            rankingbattleCount++;
                        }

                        if (action.InnerAction is RankingBattle5 rankingbattleAction5)
                        {
                            rankingbattleCount++;
                        }

                        if (action.InnerAction is RankingBattle4 rankingbattleAction4)
                        {
                            rankingbattleCount++;
                        }

                        if (action.InnerAction is RankingBattle3 rankingbattleAction3)
                        {
                            rankingbattleCount++;
                        }

                        if (action.InnerAction is RankingBattle2 rankingbattleAction2)
                        {
                            rankingbattleCount++;
                        }

                        if (action.InnerAction is RankingBattle0 rankingbattleAction0)
                        {
                            rankingbattleCount++;
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
