using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Bencodex.Json;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.RocksDBStore;
using Libplanet.Store;
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
            [Option('t', Description = "A JSON file path of tx.")]
            string txPath,
            [Option('s', Description = "An absolute path of block storage.(rocksdb)")]
            string storePath,
            [Option('i', Description = "Target block height to evaluate tx. Tip as default. (Min: 1)" +
                                       "If you set 100, using block 99 as previous state.")]
            long blockIndex = 1,
            [Option('v', Description = "Verbose mode.")]
            bool verbose = false,
            [Option('o', Description = "An path of output file.")]
            string outputPath = "")
        {
            var (outputFs, outputSw) =
                GetOutputFileStream(outputPath, "replay-tx-output.log");
            var disposables = new List<IDisposable?> { outputFs, outputSw };
            try
            {
                if (blockIndex < 1)
                {
                    throw new CommandExitedException(
                        "BLOCK-INDEX must be greater than or equal to 1.",
                        -1
                    );
                }

                var tx = LoadTx(txPath);
                var msg = $"tx id: {tx.Id}";
                _console.Out.WriteLine(msg);
                outputSw?.WriteLine(msg);

                var (store, stateStore, blockChain) = LoadBlockchain(storePath);
                disposables.Add(store);
                disposables.Add(stateStore);
                var previousBlock = blockChain[blockIndex - 1];
                var targetBlock = blockChain[blockIndex];
                if (verbose)
                {
                    msg = $"previous block({previousBlock.Index}) hash: {previousBlock.Hash}";
                    _console.Out.WriteLine(msg);
                    outputSw?.WriteLine(msg);
                    msg = $"target block({targetBlock.Index}) hash: {targetBlock.Hash}";
                    _console.Out.WriteLine(msg);
                    outputSw?.WriteLine(msg);
                }

                // Evaluate tx.
                IAccountStateDelta previousStates = new AccountStateDeltaImpl(
                    addresses => blockChain.GetStates(
                        addresses,
                        previousBlock.Hash,
                        StateCompleters<NCAction>.Reject),
                    (address, currency) => blockChain.GetBalance(
                        address,
                        currency,
                        previousBlock.Hash,
                        FungibleAssetStateCompleters<NCAction>.Reject),
                    currency => blockChain.GetTotalSupply(
                        currency,
                        previousBlock.Hash,
                        TotalSupplyStateCompleters<NCAction>.Reject),
                    tx.Signer);
                var actions = tx.SystemAction is { } sa
                    ? ImmutableList.Create(sa)
                    : ImmutableList.CreateRange(tx.CustomActions!.Cast<IAction>());
                var actionEvaluations = EvaluateActions(
                    genesisHash: blockChain.Genesis.Hash,
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
                        if (verbose)
                        {
                            msg = $"action #{actionNum} exception: {e}";
                            _console.Out.WriteLine(msg);
                            outputSw?.WriteLine(msg);
                        }

                        LoggingAboutIncompleteBlockStatesException(
                            e,
                            blockIndex - 1,
                            storePath,
                            outputSw,
                            "Tip: ");

                        continue;
                    }

                    if (actionEvaluation.Action is NCAction nca)
                    {
                        var type = nca.InnerAction.GetType();
                        var actionType = ActionTypeAttribute.ValueOf(type);
                        msg = $"- action #{actionNum}: {type.Name}(\"{actionType}\")";
                        _console.Out.WriteLine(msg);
                        outputSw?.WriteLine(msg);
                    }

                    var states = actionEvaluation.OutputStates;
                    var addressNum = 1;
                    foreach (var updatedAddress in states.UpdatedAddresses)
                    {
                        if (verbose)
                        {
                            var updatedState = states.GetState(updatedAddress);
                            msg = $"- action #{actionNum} updated address #{addressNum}({updatedAddress}) beginning..";
                            _console.Out.WriteLine(msg);
                            outputSw?.WriteLine(msg);
                            msg = $"{updatedState}";
                            _console.Out.WriteLine(msg);
                            outputSw?.WriteLine(msg);
                            msg = $"- action #{actionNum} updated address #{addressNum}({updatedAddress}) end..";
                            _console.Out.WriteLine(msg);
                            outputSw?.WriteLine(msg);
                        }

                        addressNum++;
                    }

                    actionNum++;
                }

                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                outputSw?.WriteLine(Encoding.UTF8.GetBytes(e.ToString()));
                return -1;
            }
            finally
            {
                foreach (var disposable in disposables)
                {
                    disposable?.Dispose();
                }

                disposables.Clear();
            }
        }

        [Command(Description = "Evaluate blocks and check state root hash")]
        public int Blocks(
            [Option('s', Description = "An absolute path of block storage.(rocksdb)")]
            string storePath,
            [Option('i', Description = "Target start block height. Tip as default. (Min: 1)")]
            long startIndex = 1,
            [Option('e', Description = "Target end block height. Tip as default. (Min: 1)" +
                                       "If not set, same as argument \"-i\".")]
            long? endIndex = null,
            [Option('r', Description = "Repeat count. (Min: 1)" +
                                       "If not set, default is 1.")]
            int repeatCount = 1,
            [Option('v', Description = "Verbose mode.")]
            bool verbose = false,
            [Option('o', Description = "The path of output file.")]
            string outputPath = "")
        {
            var (outputFs, outputSw) =
                GetOutputFileStream(outputPath, "replay-blocks-output.log");
            var disposables = new List<IDisposable?> { outputSw, outputFs };
            try
            {
                if (startIndex < 1)
                {
                    throw new CommandExitedException(
                        "START-INDEX must be greater than or equal to 1.",
                        -1
                    );
                }

                if (!endIndex.HasValue)
                {
                    endIndex = startIndex;
                }
                else if (endIndex < startIndex)
                {
                    throw new CommandExitedException(
                        "END-INDEX must be greater than or equal to START-INDEX.",
                        -1
                    );
                }

                var msg = $"Replay blocks start from #{startIndex} to #{endIndex}." +
                          $"(range: {endIndex - startIndex + 1}, repeat: {repeatCount})";
                _console.Out.WriteLine(msg);
                outputSw?.WriteLine(msg);

                if (verbose)
                {
                    msg = $"Local block protocol version(bpv): {BlockMetadata.CurrentProtocolVersion}";
                    _console.Out.WriteLine(msg);
                    outputSw?.WriteLine(msg);
                }

                var (store, stateStore, blockChain) = LoadBlockchain(storePath);
                disposables.Add(store);
                disposables.Add(stateStore);
                var currentBlockIndex = startIndex;
                while (currentBlockIndex <= endIndex)
                {
                    var block = blockChain[currentBlockIndex++];
                    if (verbose)
                    {
                        msg = $"- block #{block.Index} evaluating start: bpv({block.ProtocolVersion})" +
                              $", bloc-hash({block.Hash}), state-root-hash({block.StateRootHash})" +
                              $", tx-count({block.Transactions.Count})";
                        _console.Out.WriteLine(msg);
                        outputSw?.WriteLine(msg);
                    }

                    for (var i = 0; i < repeatCount; i++)
                    {
                        if (verbose)
                        {
                            msg = $"-- repeat {i + 1}/{repeatCount}..";
                            _console.Out.Write(msg);
                            outputSw?.Write(msg);
                        }

                        try
                        {
                            var actionEvaluations = blockChain.ExecuteActions(
                                block,
                                StateCompleterSet<NCAction>.Reject);

                            if (verbose)
                            {
                                msg = "(o)";
                                _console.Out.WriteLine(msg);
                                outputSw?.WriteLine(msg);
                                LoggingActionEvaluations(actionEvaluations, outputSw);
                            }
                        }
                        catch (InvalidBlockStateRootHashException)
                        {
                            if (!verbose)
                            {
                                throw;
                            }

                            msg = "(x)";
                            _console.Out.WriteLine(msg);
                            outputSw?.WriteLine(msg);

                            var actionEvaluator = GetActionEvaluator(
                                blockChain,
                                stateStore,
                                blockChain.Genesis.Hash);
                            var actionEvaluations = actionEvaluator.Evaluate(
                                block,
                                StateCompleterSet<NCAction>.Reject);
                            LoggingActionEvaluations(actionEvaluations, outputSw);

                            msg = $"- block #{block.Index} evaluating failed with ";
                            _console.Out.Write(msg);
                            outputSw?.Write(msg);

                            throw;
                        }
                        catch (Exception)
                        {
                            if (!verbose)
                            {
                                throw;
                            }

                            _console.Out.Write("x\n");
                            outputSw?.Write("x\n");
                            throw;
                        }
                    }
                }

                msg = "Replay blocks end successfully.";
                _console.Out.WriteLine(msg);
                outputSw?.WriteLine(msg);

                return 0;
            }
            catch (Exception e)
            {
                _console.Error.WriteLine(e);
                outputSw?.WriteLine(e);
                var msg = "Replay blocks end with exception.";
                _console.Out.WriteLine(msg);
                outputSw?.WriteLine(msg);
                return -1;
            }
            finally
            {
                foreach (var disposable in disposables)
                {
                    disposable?.Dispose();
                }

                disposables.Clear();
            }
        }

        private static (FileStream? fs, StreamWriter? sw) GetOutputFileStream(
            string outputPath,
            string defaultFileName)
        {
            FileStream? outputFs = null;
            StreamWriter? outputSw = null;
            if (string.IsNullOrEmpty(outputPath))
            {
                return (outputFs, outputSw);
            }

            var fileName = Path.GetFileName(outputPath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = defaultFileName;
                outputPath = Path.Combine(outputPath, fileName);
            }

            Directory.CreateDirectory(outputPath.Replace(fileName, string.Empty));
            outputFs = new FileStream(outputPath, FileMode.Create);
            outputSw = new StreamWriter(outputFs);

            return (outputFs, outputSw);
        }

        private (
            IStore store,
            IStateStore stateStore,
            BlockChain<NCAction> blockChain) LoadBlockchain(string storePath)
        {
            // Load store and genesis block.
            if (!Directory.Exists(storePath))
            {
                throw new CommandExitedException(
                    $"The given STORE-PATH, {storePath} does not found.",
                    -1);
            }

            var store = StoreType.RocksDb.CreateStore(storePath);
            if (store.GetCanonicalChainId() is not { } chainId)
            {
                throw new CommandExitedException(
                    $"There is no canonical chain: {storePath}",
                    -1);
            }

            var genesisBlockHash = store.IndexBlockHash(chainId, 0) ??
                                   throw new CommandExitedException(
                                       $"The given blockIndex {0} does not found", -1);
            var genesisBlock = store.GetBlock<NCAction>(genesisBlockHash);

            // Make BlockChain and blocks.
            var policy = new BlockPolicySource(Logger.None).GetPolicy();
            var stagePolicy = new VolatileStagePolicy<NCAction>();
            var stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(storePath, "states"));
            var stateStore = new TrieStateStore(stateKeyValueStore);
            return (
                store,
                stateStore,
                new BlockChain<NCAction>(
                    policy,
                    stagePolicy,
                    store,
                    stateStore,
                    genesisBlock));
        }

        private Transaction<NCAction> LoadTx(string txPath)
        {
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

            return new Transaction<NCAction>(txDict);
        }

        private ActionEvaluator<NCAction> GetActionEvaluator(
            BlockChain<NCAction> blockChain,
            IStateStore stateStore,
            BlockHash genesisBlockHash)
        {
            var policy = new BlockPolicySource(Logger.None).GetPolicy();
            return new ActionEvaluator<NCAction>(
                policy.BlockAction,
                blockChainStates: blockChain,
                trieGetter: hash => stateStore.GetStateRoot(blockChain[hash].StateRootHash),
                genesisHash: genesisBlockHash,
                nativeTokenPredicate: policy.NativeTokens.Contains);
        }

        private void LoggingAboutIncompleteBlockStatesException(
            Exception? e,
            long blockIndex,
            string storePath,
            TextWriter? textWriter,
            string prefix = "")
        {
            if (e is not UnexpectedlyTerminatedActionException
                {
                    InnerException: IncompleteBlockStatesException
                })
            {
                return;
            }

            var msg = $"{prefix}Block #{blockIndex} of the blockchain store does not contain `state`." +
                      $" You can check your store like below.\n" +
                      $"  `dotnet run -- state check {blockIndex} -s {storePath}`";
            _console.Out.WriteLine(msg);
            textWriter?.WriteLine(msg);
        }

        private void LoggingActionEvaluations(
            IReadOnlyList<ActionEvaluation> actionEvaluations,
            TextWriter? textWriter)
        {
            var count = actionEvaluations.Count;
            for (var i = 0; i < count; i++)
            {
                var actionEvaluation = actionEvaluations[i];
                var actionType = actionEvaluation.Action is NCAction nca
                    ? ActionTypeAttribute.ValueOf(nca.InnerAction.GetType())
                    : actionEvaluation.Action.GetType().Name;
                var prefix = $"--- action evaluation {i + 1}/{count}:";
                var msg = prefix +
                          $" tx-id({actionEvaluation.InputContext.TxId})" +
                          $", action-type(\"{actionType}\")";
                if (actionEvaluation.Exception is null)
                {
                    msg += ", no-exception";
                    _console.Out.WriteLine(msg);
                    textWriter?.WriteLine(msg);

                    continue;
                }

                msg += ", exception below";
                _console.Out.WriteLine(msg);
                textWriter?.WriteLine(msg);
                msg = $"---- {actionEvaluation.Exception}";
                _console.Out.WriteLine(msg);
                textWriter?.WriteLine(msg);
            }
        }
    }
}
