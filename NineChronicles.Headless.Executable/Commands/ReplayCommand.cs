using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bencodex.Json;
using Bencodex.Types;
using Cocona;
using Cocona.Help;
using Libplanet;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Extensions.RemoteBlockChainStates;
using Libplanet.RocksDBStore;
using Libplanet.State;
using Libplanet.Store;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Action.Loader;
using Nekoyume.Blockchain.Policy;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Executable.Store;
using Serilog;
using Serilog.Core;
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

                var rawTx =
                    "ZDE6UzcwOjBEAiBVkOdmDWVpSIQeQpQWBwK51nXDGBteyKu87J1kLzLWeAIgclF43hCspJTKTsoFKZVlb3joeqBdaZ8upgl5qGmVasQxOmFsZHU3OnR5cGVfaWR1MTY6aGFja19hbmRfc2xhc2gyMXU2OnZhbHVlc2R1MTI6YXBTdG9uZUNvdW50dTE6MHUxMzphdmF0YXJBZGRyZXNzMjA6wtwchey4hAGg5G3HWS1e3hz2g+x1ODpjb3N0dW1lc2xldTEwOmVxdWlwbWVudHNsMTY6NSGmOjLtwEiNlficEAqJ1DE2OjapPFbOWu5HmhLdztJH10gxNjpqdEReBSapRJ/L7bO39+BxMTY63GpKr3qc+EC22JdY8DI9hzE2OiF3iLj1r3ZHmC268CauwVsxNjrm7wn2vFbfTZQAiq9nnTFlZXU1OmZvb2RzbGV1MjppZDE2OpIhoddkn2lLgi/5JKiWP0N1MTpybGx1MTowdTU6MzAwMDFlZXU3OnN0YWdlSWR1MzoxMDh1MTQ6dG90YWxQbGF5Q291bnR1MToxdTc6d29ybGRJZHUxOjNlZWUxOmczMjpFgiUNDaM7BneahHXSg9XdIQxoO5uZnXTQP6xPWPprzjE6bGkxZTE6bWxkdTEzOmRlY2ltYWxQbGFjZXMxOhJ1NzptaW50ZXJzbnU2OnRpY2tlcnU0Ok1lYWRlaTEwMDAwMDAwMDAwMDAwMDAwMDBlZTE6bmkxMzk4ZTE6cDY1OgShAQTYvayhfMAwpWFaJ5x+xA2v01VDMs5RRlkHQ6gYJo7fVVlVJwkJuTMN6kiajvdb32HRXiKAynmRSC7vXcSRMTpzMjA69hqiNWbgMccTKNT7oRjzYPQlMiAxOnR1Mjc6MjAyMy0wNy0wNFQwMjoyODo0NS42NjMzNDNaMTp1bGVl";
                // rawTx from graphql query https://9c-main-validator-1.nine-chronicles.com/graphql/explorer
                // query {
                // transactionQuery {
                //    transaction(id: "27df384448b9b3972240e5ce17dc845dcfac12f0b10f6cc213fa02d4beeba37b") {
                //        serializedPayload
                //    }
                //}
                // https://9cscan.com/tx/9ae64e1ad2e8c64494adbc46a1b8a5595646298e4c15ee3a51256803d13b8ac7
                rawTx =
                    "ZDE6UzcwOjBEAiAUKdHuJEm9Pz/aDjdzDLAgWNwK+GXr0McCVOMQ30MRDgIgWD+eozx39VkTALagD15vx6Fgi0ZAlkWLRNv2zakxg+QxOmFsZHU3OnR5cGVfaWR1MTY6aGFja19hbmRfc2xhc2gyMXU2OnZhbHVlc2R1MTI6YXBTdG9uZUNvdW50dTE6MHUxMzphdmF0YXJBZGRyZXNzMjA6t/WYFfOVeNzLC9xV2Uzl7mac9PN1ODpjb3N0dW1lc2xldTEwOmVxdWlwbWVudHNsMTY61nlsSqODfUOOEszOAlkH5jE2OuxMS1SATxpLsjflHFjke6AxNjo6Ip1eT5YlSZs2p+I/Tj7yMTY6NwJVgHmoZEGXGWnemRNWhTE2OgNpJZstK5lDmoNeQAn9pJ0xNjpjoICqQFlaTZLj0TlFF4RsZXU1OmZvb2RzbGV1MjppZDE2OsyK7y05Vy9NnVy9w124Hth1MTpybGx1MTowdTU6MzAwMDFlZXU3OnN0YWdlSWR1MzoxMzJ1MTQ6dG90YWxQbGF5Q291bnR1MToxdTc6d29ybGRJZHUxOjNlZWUxOmczMjpFgiUNDaM7BneahHXSg9XdIQxoO5uZnXTQP6xPWPprzjE6bGkxZTE6bWxkdTEzOmRlY2ltYWxQbGFjZXMxOhJ1NzptaW50ZXJzbnU2OnRpY2tlcnU0Ok1lYWRlaTEwMDAwMDAwMDAwMDAwMDAwMDBlZTE6bmkxMTY4ZTE6cDY1OgSfrTZuXHbkuF7xIiUCv+c6p/LOGcRDsgNR721m2dGVrigf4xj9y9KrFqWwRcrOdFxhBnKN9es9Gt1JId1mGyAlMTpzMjA6Bzk4jaVfoMkFuRyJ1O1sHqATgd8xOnR1Mjc6MjAyMy0wNy0yNFQwOTo0OTo0OS4wMDAxNzdaMTp1bGVl";
                byte[] bytes = Convert.FromBase64String(rawTx);
                var tx = Transaction.Deserialize(bytes);
                var msg = $"tx id: {tx.Id}";
                _console.Out.WriteLine(msg);
                outputSw?.WriteLine(msg);

#pragma warning disable S1075
                var (store, stateStore, blockChain) = LoadBlockchain(storePath, "https://9c-main-full-state.nine-chronicles.com/graphql/explorer");
#pragma warning restore S1075
                disposables.Add(store);
                disposables.Add(stateStore);
                // var previousBlock = blockChain[blockIndex - 1];
                // var targetBlock = blockChain[blockIndex];
                var previousBlockIndex = 7337842L;
                var previousBlockHash =
                    new BlockHash(ByteUtil.ParseHex("2e19f17270fd1b2dd7e2eac3d858485f03e2fa0982acb831a8ecb94f0fca68c2"));
                var targetBlockIndex = blockIndex;
                var targetBlockHash =
                    new BlockHash(
                        ByteUtil.ParseHex("583dc63f730230755da0c529f34578487cdef670192b6a9aac285b312c7302a4"));
                if (verbose)
                {
                    msg = $"previous block({previousBlockIndex}) hash: {previousBlockHash}";
                    _console.Out.WriteLine(msg);
                    outputSw?.WriteLine(msg);
                    msg = $"target block({targetBlockIndex}) hash: {targetBlockHash}";
                    _console.Out.WriteLine(msg);
                    outputSw?.WriteLine(msg);
                }

                // Evaluate tx.
                IAccountState previousBlockStates = blockChain.GetBlockState(previousBlockHash);
                IAccountStateDelta previousStates = AccountStateDelta.Create(previousBlockStates);
                var actions = tx.Actions.Select(a => ToAction(a));
                var actionEvaluations = EvaluateActions(
                    preEvaluationHash: HashDigest<SHA256>.FromString("2e19f17270fd1b2dd7e2eac3d858485f03e2fa0982acb831a8ecb94f0fca68c2"),
                    blockIndex: targetBlockIndex,
                    blockProtocolVersion: blockChain.Tip.ProtocolVersion,
                    txid: tx.Id,
                    previousStates: previousStates,
                    miner: new Address("0x7655122899ccd2cc87b38a35d7158c9516504119"),
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
                        var actionType = ActionTypeAttribute.ValueOf(type);
                        msg = $"- action #{actionNum}: {type.Name}(\"{actionType}\")";
                        _console.Out.WriteLine(msg);
                        outputSw?.WriteLine(msg);
                    }

                    var states = actionEvaluation.OutputState;
                    var addressNum = 1;
                    foreach (var (updatedAddress, updatedState) in states.Delta.States)
                    {
                        if (verbose)
                        {
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
                // startIndex = 7140408L;
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
                    endIndex = blockChain.Tip.Index;
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
                                out IReadOnlyList<IActionEvaluation> actionEvaluations);

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

                            var actionEvaluator = GetActionEvaluator(blockChain);
                            var actionEvaluations = actionEvaluator.Evaluate(block);
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
            BlockChain blockChain) LoadBlockchain(string storePath, string uri = null!)
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
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Console().CreateLogger();
            var policy = new BlockPolicySource(Log.Logger).GetPolicy();
            var stagePolicy = new VolatileStagePolicy();
            var stateKeyValueStore = new RocksDBKeyValueStore(Path.Combine(storePath, "states"));
            var stateStore = new TrieStateStore(stateKeyValueStore);
            IBlockChainStates blockChainStates;
            if (!string.IsNullOrEmpty(uri))
            {
                blockChainStates = new RemoteBlockChainStates(new Uri(uri));
            }
            else
            {
                blockChainStates = new BlockChainStates(store, stateStore);
            }
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                blockChainStates,
                new NCActionLoader(),
                null);
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

        private ActionEvaluator GetActionEvaluator(BlockChain blockChain)
        {
            var policy = new BlockPolicySource(Logger.None).GetPolicy();
            IActionLoader actionLoader = new NCActionLoader();
            return new ActionEvaluator(
                _ => policy.BlockAction,
                blockChainStates: blockChain,
                actionTypeLoader: actionLoader,
                feeCalculator: null);
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
            IReadOnlyList<IActionEvaluation> actionEvaluations,
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
                    actionType = ActionTypeAttribute.ValueOf(action.GetType())?.ToString();
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
                var delta = HashDigest<SHA256>.DeriveFrom(actionEvaluation.Serialize());
                var msg = prefix +
                          $" tx-id({actionEvaluation.InputContext.TxId})" +
                          $", action-type(\"{actionType}\")" +
                          $", delta: {delta}";
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
