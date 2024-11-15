using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Types.Tx;
using Serilog;
using Libplanet.Types.Evidence;
using Libplanet.Types.Blocks;
using Libplanet.Types.Assets;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionContext.cs.
        /// </summary>
        private sealed class ActionContext : IActionContext
        {
            public ActionContext(
                Address signer,
                TxId? txid,
                Address miner,
                long blockIndex,
                int blockProtocolVersion,
                IWorld previousState,
                int randomSeed,
                bool rehearsal = false)
            {
                Signer = signer;
                TxId = txid;
                Miner = miner;
                BlockIndex = blockIndex;
                BlockProtocolVersion = blockProtocolVersion;
                Rehearsal = rehearsal;
                PreviousState = previousState;
                RandomSeed = randomSeed;
            }

            public Address Signer { get; }

            public TxId? TxId { get; }

            public Address Miner { get; }

            public long BlockIndex { get; }

            public int BlockProtocolVersion { get; }

            public BlockCommit? LastCommit { get; }

            public bool Rehearsal { get; }

            public IWorld PreviousState { get; }

            public int RandomSeed { get; }

            public bool IsPolicyAction => TxId is null;

            // NOTE: Replay does not support block actions.
            public IReadOnlyList<ITransaction> Txs => ImmutableList<ITransaction>.Empty;

            public IReadOnlyList<EvidenceBase> Evidence => ImmutableList<EvidenceBase>.Empty;

            public void UseGas(long gas)
            {
            }

            public FungibleAssetValue? MaxGasPrice => null;

            public IRandom GetRandom() => new Random(RandomSeed);
        }

        private sealed class Random : System.Random, IRandom
        {
            public Random(int seed)
                : base(seed)
            {
                Seed = seed;
            }

            public int Seed { get; private set; }
        }

        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionEvaluator.cs#L286.
        /// </summary>
        private static IEnumerable<ActionEvaluation> EvaluateActions(
            HashDigest<SHA256> preEvaluationHash,
            long blockIndex,
            int blockProtocolVersion,
            TxId? txid,
            IWorld previousStates,
            Address miner,
            Address signer,
            byte[] signature,
            IImmutableList<IAction> actions,
            ILogger? logger = null)
        {
            ActionContext CreateActionContext(
                IWorld prevState,
                int randomSeed)
            {
                return new ActionContext(
                    signer: signer,
                    txid: txid,
                    miner: miner,
                    blockIndex: blockIndex,
                    blockProtocolVersion: blockProtocolVersion,
                    previousState: prevState,
                    randomSeed: randomSeed);
            }

            byte[] preEvaluationHashBytes = preEvaluationHash.ToByteArray();
            int seed = ActionEvaluator.GenerateRandomSeed(preEvaluationHashBytes, signature, 0);

            IWorld states = previousStates;
            foreach (IAction action in actions)
            {
                Exception? exc = null;
                IWorld nextStates = states;
                ActionContext context = CreateActionContext(nextStates, seed);

                try
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    nextStates = action.Execute(context);
                    logger?
                        .Information(
                            "Action {Action} took {DurationMs} ms to execute",
                            action,
                            stopwatch.ElapsedMilliseconds);
                }
                catch (OutOfMemoryException e)
                {
                    // Because OutOfMemory is thrown non-deterministically depending on the state
                    // of the node, we should throw without further handling.
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash.ByteArray));
                    throw;
                }
                catch (Exception e)
                {
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash.ByteArray));
                    var innerMessage =
                        $"The action {action} (block #{blockIndex}, " +
                        $"pre-evaluation hash {ByteUtil.Hex(preEvaluationHash.ByteArray)}, " +
                        $"tx {txid} threw an exception during execution.  " +
                        "See also this exception's InnerException property";
                    logger?.Error(
                        "{Message}\nInnerException: {ExcMessage}", innerMessage, e.Message);
                    exc = new UnexpectedlyTerminatedActionException(
                        innerMessage,
                        preEvaluationHash,
                        blockIndex,
                        txid,
                        null,
                        action,
                        e);
                }

                // As IActionContext.Random is stateful, we cannot reuse
                // the context which is once consumed by Execute().
                ActionContext equivalentContext = CreateActionContext(states, seed);

                yield return new ActionEvaluation(
                    action: action,
                    inputContext: equivalentContext,
                    outputState: nextStates,
                    exception: exc);

                if (exc is { })
                {
                    yield break;
                }

                states = nextStates;
                unchecked
                {
                    seed++;
                }
            }
        }
    }
}
