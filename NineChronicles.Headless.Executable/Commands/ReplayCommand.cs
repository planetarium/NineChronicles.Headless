using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bencodex;
using Bencodex.Json;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Extensions.RemoteBlockChainStates;
using Libplanet.Types.Blocks;
using Libplanet.RocksDBStore;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Blockchain.Policy;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Store;
using static NineChronicles.Headless.NCActionUtils;

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
                IWorld previousWorld = new World(blockChain.GetWorldState(previousBlock.Hash));
                var actions = tx.Actions.Select(a => ToAction(a));
                var actionEvaluations = EvaluateActions(
                    preEvaluationHash: targetBlock.PreEvaluationHash,
                    blockIndex: targetBlock.Index,
                    blockProtocolVersion: targetBlock.ProtocolVersion,
                    txid: tx.Id,
                    previousStates: previousWorld,
                    miner: targetBlock.Miner,
                    signer: tx.Signer,
                    signature: tx.Signature,
                    actions: actions.Cast<IAction>().ToImmutableList()
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

                    if (actionEvaluation.Action is ActionBase nca)
                    {
                        var type = nca.GetType();
                        var actionType = type.GetCustomAttribute<ActionTypeAttribute>()?.TypeIdentifier;
                        msg = $"- action #{actionNum}: {type.Name}(\"{actionType}\")";
                        _console.Out.WriteLine(msg);
                        outputSw?.WriteLine(msg);
                    }

                    var inputState = actionEvaluation.InputContext.PreviousState;
                    var outputState = actionEvaluation.OutputState;
                    var accountDiff = AccountDiff.Create(inputState.Trie, outputState.Trie);

                    var states = actionEvaluation.OutputState;
                    var addressNum = 1;
                    foreach (var (updatedAddress, stateDiff) in accountDiff.StateDiffs)
                    {
                        if (verbose)
                        {
                            msg = $"- action #{actionNum} updated value at address #{addressNum} ({updatedAddress})";
                            _console.Out.WriteLine(msg);
                            outputSw?.WriteLine(msg);
                            msg = $"  from {stateDiff.Item1} to {stateDiff.Item2}";
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
            long? startIndex = null,
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
                var (store, stateStore, blockChain) = LoadBlockchain(storePath);
                disposables.Add(store);
                disposables.Add(stateStore);
                startIndex ??= blockChain.Tip.Index;

                if (startIndex is null or < 1)
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

                var currentBlockIndex = startIndex.Value;
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
                            var rootHash = blockChain.DetermineBlockStateRootHash(block,
                                out IReadOnlyList<ICommittedActionEvaluation> actionEvaluations);

                            if (verbose)
                            {
                                msg = "(o)";
                                _console.Out.WriteLine(msg);
                                outputSw?.WriteLine(msg);
                                LoggingActionEvaluations(actionEvaluations, outputSw);
                            }

                            if (!rootHash.Equals(block.StateRootHash))
                            {
                                throw new InvalidBlockStateRootHashException(
                                    $"Expected {block.StateRootHash} but {rootHash}",
                                    block.StateRootHash,
                                    rootHash);
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

                            var actionEvaluator = GetActionEvaluator(stateStore);
                            var actionEvaluations = blockChain.DetermineBlockStateRootHash(block,
                                out IReadOnlyList<ICommittedActionEvaluation> failedActionEvaluations);
                            LoggingActionEvaluations(failedActionEvaluations, outputSw);

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

        [Command(Description = "Evaluate transaction with remote states")]
        public int RemoteTx(
            [Option("tx", new[] { 't' }, Description = "The transaction id")]
            string transactionId,
            [Option("endpoint", new[] { 'e' }, Description = "GraphQL endpoint to get remote state")]
            string endpoint,
            [Option(
                "cache-directory",
                new [] { 'c' },
                Description = "A directory to store states, balances, etc as cache")]
            string? cacheDirectory=null)
        {
            var graphQlClient = new GraphQLHttpClient(new Uri(endpoint), new SystemTextJsonSerializer());
            var transactionResponse = GetTransactionData(graphQlClient, transactionId);
            if (transactionResponse is null)
            {
                throw new CommandExitedException(
                    $"Failed to query transaction and transactionResult with id {transactionId}", -1);
            }

            var transaction = GetTransactionFromQueryResult(transactionResponse);
            if (transaction is null)
            {
                throw new CommandExitedException("Failed to get transaction from query", -1);
            }

            var transactionResult = transactionResponse.Transaction?.TransactionResult;
            if (transactionResult?.BlockHash is null)
            {
                throw new CommandExitedException("Failed to get transactionResult from query", -1);
            }

            var block = GetBlockData(graphQlClient, transactionResult.BlockHash)?.ChainQuery?.BlockQuery?.Block;
            var previousBlockHashValue = block?.PreviousBlock?.Hash;
            var preEvaluationHashValue = block?.PreEvaluationHash;
            var minerValue = block?.Miner;
            if (previousBlockHashValue is null || preEvaluationHashValue is null || minerValue is null)
            {
                throw new CommandExitedException("Failed to get block from query", -1);
            }
            var miner = new Address(minerValue);

            var explorerEndpoint = $"{endpoint}/explorer";
            var blockChainStates = new LocalCacheBlockChainStates(
                new RemoteBlockChainStates(new Uri(explorerEndpoint)),
                cacheDirectory ?? Path.Join(Path.GetTempPath(), "ncd-replay-remote-tx-cache"));

            var previousBlockHash = BlockHash.FromString(previousBlockHashValue);
            var previousStates = new World(blockChainStates.GetWorldState(previousBlockHash));

            var actions = transaction.Actions
                .Select(ToAction)
                .Cast<IAction>()
                .ToImmutableList();
            var actionEvaluations = EvaluateActions(
                preEvaluationHash: HashDigest<SHA256>.FromString(preEvaluationHashValue),
                blockIndex: transactionResult.BlockIndex ?? 0,
                blockProtocolVersion: 0,
                txid: transaction.Id,
                previousStates: previousStates,
                miner: miner,
                signer: transaction.Signer,
                signature: transaction.Signature,
                actions: actions);

            actionEvaluations
                .Select((evaluation, index) => (evaluation, index))
                .ToList()
                .ForEach(x => PrintEvaluation(x.evaluation, x.index));

            return 0;
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
            BlockChain blockChain) LoadBlockchain(string storePath)
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
            var genesisBlock = store.GetBlock(genesisBlockHash);

            // Make BlockChain and blocks.
            var policy = new BlockPolicySource().GetPolicy();
            var stagePolicy = new VolatileStagePolicy();
            var stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(storePath, "states"));
            var stateStore = new TrieStateStore(stateKeyValueStore);
            var blockChainStates = new BlockChainStates(store, stateStore);
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                stateStore,
                new NCActionLoader());
            return (
                store,
                stateStore,
                new BlockChain(
                    policy,
                    stagePolicy,
                    store,
                    stateStore,
                    genesisBlock,
                    blockChainStates,
                    actionEvaluator));
        }

        private Transaction LoadTx(string txPath)
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

            return TxMarshaler.UnmarshalTransaction(txDict);
        }

        private ActionEvaluator GetActionEvaluator(IStateStore stateStore)
        {
            var policy = new BlockPolicySource().GetPolicy();
            IActionLoader actionLoader = new NCActionLoader();
            return new ActionEvaluator(
                _ => policy.BlockAction,
                stateStore: stateStore,
                actionTypeLoader: actionLoader);
        }

        private void LoggingAboutIncompleteBlockStatesException(
            Exception? e,
            long blockIndex,
            string storePath,
            TextWriter? textWriter,
            string prefix = "")
        {
            // FIXME: IncompleteBlockStatesException has been removed.
            // Probably need a proper fix.
            if (e is not UnexpectedlyTerminatedActionException
                {
                    InnerException: ArgumentException
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
            IReadOnlyList<ICommittedActionEvaluation> actionEvaluations,
            TextWriter? textWriter)
        {
            var count = actionEvaluations.Count;
            for (var i = 0; i < count; i++)
            {
                var actionEvaluation = actionEvaluations[i];
                ActionBase? DecodeAction(IValue plainValue)
                {
                    try
                    {
                        return NCActionUtils.ToAction(plainValue);
                    }
                    catch
                    {
                        return null;
                    }
                }

                string? actionType;
                if (DecodeAction(actionEvaluation.Action) is { } action)
                {
                    actionType = action.GetType().GetCustomAttribute<ActionTypeAttribute>()?.TypeIdentifier?.ToString();
                }
                else if (actionEvaluation.Action is Dictionary dictionary && dictionary.ContainsKey("type_id"))
                {
                    actionType = dictionary["type_id"].ToString();
                }
                else
                {
                    actionType = actionEvaluation.Action.GetType().Name;
                }

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

        private Transaction? GetTransactionFromQueryResult(GetTransactionDataResponse query)
        {
            var bencodexTransaction = query.ChainQuery?.TransactionQuery?.Transaction?.SerializedPayload;
            if (bencodexTransaction is null)
            {
                return null;
            }

            var serializedTransaction = new Codec().Decode(Convert.FromBase64String(bencodexTransaction));

            return TxMarshaler.UnmarshalTransaction((Dictionary)serializedTransaction);
        }

        private void PrintEvaluation(ActionEvaluation evaluation, int index)
        {
            var exception = evaluation.Exception?.InnerException ?? evaluation.Exception;
            if (exception is not null)
            {
                _console.Out.WriteLine($"action #{index} exception: {exception}");
                return;
            }

            if (evaluation.Action is ActionBase nca)
            {
                var type = nca.GetType();
                var actionType = type.GetCustomAttribute<ActionTypeAttribute>()?.TypeIdentifier;
                _console.Out.WriteLine($"- action #{index + 1}: {type.Name}(\"{actionType}\")");
            }

            var inputState = evaluation.InputContext.PreviousState;
            var outputState = evaluation.OutputState;
            var accountDiff = AccountDiff.Create(inputState.Trie, outputState.Trie);
            foreach (var (updatedAddress, stateDiff, addressIndex) in accountDiff.StateDiffs.Select((x, i) => (x.Key, x.Value, i)))
            {
                _console.Out.WriteLine($"- action #{index + 1} updated value at address #{addressIndex + 1} ({updatedAddress})");
                _console.Out.WriteLine($"  from {stateDiff.Item1} to {stateDiff.Item2}");
            }
        }
    }
}
