using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bencodex.Json;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tx;
using Nekoyume.BlockChain.Policy;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Store;
using Serilog.Core;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        // private static readonly Codec _codec = new Codec();
        private readonly IConsole _console;

        public ReplayCommand(IConsole console)
        {
            _console = console;
        }

        [PrimaryCommand]
        public void Help([FromService] ICoconaHelpMessageBuilder helpMessageBuilder)
        {
            _console.Error.WriteLine(helpMessageBuilder.BuildAndRenderForCurrentContext());
        }

        [Command(Description = "Evaluate tx and calculate result state")]
        public int Tx(
            [Argument("TX-PATH", Description = "A JSON file path of tx.")]
            string txPath,
            [Option("STORE-PATH", Description = "An absolute path of block storage.")]
            string storePath,
            [Option("BLOCK-INDEX", Description = "Target block height to evaluate tx. Tip as default. (Min: 1)" +
                                                 "If you set 100, using block 99 as previous state.")]
            int blockIndex = 1
        )
        {
            try
            {
                if (blockIndex < 1)
                {
                    throw new CommandExitedException(
                        "BLOCK-INDEX must be greater than or equal to 1.",
                        -1
                    );
                }

                // Read json file and parse to tx.
                using var stream = new FileStream(txPath, FileMode.Open);
                stream.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[stream.Length];
                while (stream.Position < stream.Length)
                {
                    bytes[stream.Position] = (byte)stream.ReadByte();
                }

                var converter = new BencodexJsonConverter();
                var txReader = new Utf8JsonReader(bytes);
                var txValue = converter.Read(
                    ref txReader,
                    typeof(object),
                    new JsonSerializerOptions());
                if (txValue is not Dictionary txDict)
                {
                    throw new CommandExitedException(
                        $"The given json file, {txPath} is not a transaction.",
                        -1);
                }

                var tx = new Transaction<NCAction>(txDict);
                _console.Out.WriteLine($"tx id: {tx.Id}");

                // Load store and genesis block.
                if (!Directory.Exists(storePath))
                {
                    throw new CommandExitedException($"The given STORE-PATH, {storePath} does not found.", -1);
                }

                var store = StoreType.RocksDb.CreateStore(storePath);
                if (store.GetCanonicalChainId() is not { } chainId)
                {
                    throw new CommandExitedException($"There is no canonical chain: {storePath}", -1);
                }

                var genesisBlockHash = store.IndexBlockHash(chainId, 0) ??
                                       throw new CommandExitedException(
                                           $"The given blockIndex {0} does not found", -1);
                var genesisBlock = store.GetBlock<NCAction>(genesisBlockHash);
                _console.Out.WriteLine($"genesis block hash: {genesisBlock.Hash}");

                // Make BlockChain and blocks.
                var policy = new BlockPolicy<NCAction>();
                var stagePolicy = new VolatileStagePolicy<NCAction>();
                IKeyValueStore stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(storePath, "states"));
                var stateStore = new TrieStateStore(stateKeyValueStore);
                var blockChain = new BlockChain<NCAction>(
                    policy,
                    stagePolicy,
                    store,
                    stateStore,
                    genesisBlock,
                    renderers: new[] { new BlockPolicySource(Logger.None).BlockRenderer });

                var previousBlock = blockChain[blockIndex - 1];
                _console.Out.WriteLine($"previous block({previousBlock.Index}) hash: {previousBlock.Hash}");
                var targetBlock = blockChain[blockIndex];
                _console.Out.WriteLine($"target block({targetBlock.Index}) hash: {targetBlock.Hash}");

                // Evaluate tx.
                IAccountStateDelta previousStates = new AccountStateDeltaImpl(
                    addresses => blockChain.GetStates(addresses, previousBlock.Hash),
                    (address, currency) => blockChain.GetBalance(address, currency, previousBlock.Hash),
                    currency => blockChain.GetTotalSupply(currency, previousBlock.Hash),
                    tx.Signer);
                var actions = tx.SystemAction is { } sa
                    ? ImmutableList.Create(sa)
                    : ImmutableList.CreateRange(tx.CustomActions!.Cast<IAction>());
                var actionEvaluations = EvaluateActions(
                    genesisHash: genesisBlockHash,
                    preEvaluationHash: targetBlock.PreEvaluationHash,
                    blockIndex: blockIndex,
                    txid: tx.Id,
                    previousStates: previousStates,
                    miner: targetBlock.Miner,
                    signer: tx.Signer,
                    signature: tx.Signature,
                    actions: actions,
                    rehearsal: false,
                    previousBlockStatesTrie: null,
                    nativeTokenPredicate: _ => true
                );
                var actionNum = 1;
                foreach (var actionEvaluation in actionEvaluations)
                {
                    if (actionEvaluation.Exception is { } e)
                    {
                        _console.Out.WriteLine($"action #{actionNum} exception: {e}");
                        continue;
                    }

                    if (actionEvaluation.Action is NCAction nca)
                    {
                        _console.Out.WriteLine($"action #{actionNum} type: {nca.InnerAction.GetType().Name}");
                    }

                    var states = actionEvaluation.OutputStates;
                    var addressNum = 1;
                    foreach (var updatedAddress in states.UpdatedAddresses)
                    {
                        _console.Out.WriteLine($"updated address #{addressNum}: {updatedAddress}");
                        addressNum++;
                    }

                    actionNum++;
                }

                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                return -1;
            }
        }
    }
}
