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

            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            IStore store = storeType.CreateStore(storePath);
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException(
                    $"There is no canonical chain: {storePath}",
                    -1);
            }

            BlockHash tipHash = store.IndexBlockHash(chainId, -1)
                          ?? throw new CommandExitedException("The given chain seems empty.", -1);
            Block<NCAction> tip = store.GetBlock<NCAction>(blockPolicy.GetHashAlgorithm, tipHash);
            _console.Out.WriteLine(Utils.SerializeHumanReadable(tip.Header));
            (store as IDisposable)?.Dispose();
        }

        [Command(Description = "Print each block's mining time and tx stats (total tx, hack and slash, ranking battle, " +
                               "mimisbrunnr) of a given chain in csv format.")]
        public void Inspect(
            [Argument("STORE-TYPE",
                Description = "Store type of RocksDb.")]
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

            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            IStore store = storeType.CreateStore(storePath);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            if (!(store.GetCanonicalChainId() is { } chainId))
            {
                throw new CommandExitedException($"There is no canonical chain: {storePath}", -1);
            }

            if (!(store.IndexBlockHash(chainId, 0) is { } gHash))
            {
                throw new CommandExitedException($"There is no genesis block: {storePath}", -1);
            }

            Block<NCAction> genesisBlock = store.GetBlock<NCAction>(blockPolicy.GetHashAlgorithm, gHash);
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

            var typeOfActionTypeAttribute = typeof(ActionTypeAttribute);
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
                var hackAndSlashCount = 0;
                var rankingBattleCount = 0;
                var mimisbrunnrBattleCount = 0;
                foreach (var tx in block.Transactions)
                {
                    txCount++;
                    foreach (var action in tx.Actions)
                    {
                        var actionTypeAttribute =
                            Attribute.GetCustomAttribute(action.InnerAction.GetType(), typeOfActionTypeAttribute)
                                as ActionTypeAttribute;
                        if (actionTypeAttribute is null)
                        {
                            continue;
                        }

                        var typeIdentifier = actionTypeAttribute.TypeIdentifier;
                        if (typeIdentifier.StartsWith("hack_and_slash"))
                        {
                            hackAndSlashCount++;
                        }
                        else if (typeIdentifier.StartsWith("ranking_battle"))
                        {
                            rankingBattleCount++;
                        }
                        else if (typeIdentifier.StartsWith("mimisbrunnr_battle"))
                        {
                            mimisbrunnrBattleCount++;
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

            (store as IDisposable)?.Dispose();
        }
    }
}
