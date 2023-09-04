using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Bencodex.Types;
using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Action;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Libplanet.Action.State;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Serilog;
using ActionEvaluation = Libplanet.Action.ActionEvaluation;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/State/AccountStateDelta.cs.
        /// </summary>
        [Pure]
        private sealed class World : IWorld
        {
            private readonly IWorldState _baseState;

            private World(IWorldState baseState)
                : this(baseState, new WorldDelta())
            {
            }

            private World(IWorldState baseState, IWorldDelta delta)
            {
                _baseState = baseState;
                Delta = delta;
                Legacy = true;
            }
            
            public bool Legacy { get; private set; }

            /// <inheritdoc/>
            public IWorldDelta Delta { get; private set; }

            /// <inheritdoc/>
            [Pure]
            public BlockHash? BlockHash => _baseState.BlockHash;
            
            /// <summary>
            /// Creates a null state delta from given <paramref name="previousState"/>.
            /// </summary>
            /// <param name="previousState">The previous <see cref="IAccountState"/> to use as
            /// a basis.</param>
            /// <returns>A null state delta created from <paramref name="previousState"/>.
            /// </returns>
            public static IWorld Create(IWorldState previousState) =>
                new World(previousState)
                    { Legacy = previousState.Legacy };

            public IWorld SetAccount(IAccount account)
            {
                if (!account.Address.Equals(ReservedAddresses.LegacyAccount)
                    && account.Delta.UpdatedFungibleAssets.Count > 0)
                {
                    return this;
                }

                return new World(this, new WorldDelta(Delta.Accounts.SetItem(account.Address, account)))
                    { Legacy = Legacy && account.Address.Equals(ReservedAddresses.LegacyAccount) };
            }
            
            public IAccount GetAccount(Address address)
            {
                return Delta.Accounts.TryGetValue(address, out IAccount? account)
                    ? account!
                    : _baseState.GetAccount(address);
            }
        }

        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/State/AccountDelta.cs.
        /// </summary>
        private sealed class AccountDelta : IAccountDelta
        {
            internal AccountDelta()
            {
                States = ImmutableDictionary<Address, IValue>.Empty;
                Fungibles = ImmutableDictionary<(Address, Currency), BigInteger>.Empty;
                TotalSupplies = ImmutableDictionary<Currency, BigInteger>.Empty;
                ValidatorSet = null;
            }

            internal AccountDelta(
                IImmutableDictionary<Address, IValue> statesDelta,
                IImmutableDictionary<(Address, Currency), BigInteger> fungiblesDelta,
                IImmutableDictionary<Currency, BigInteger> totalSuppliesDelta,
                ValidatorSet? validatorSetDelta)
            {
                States = statesDelta;
                Fungibles = fungiblesDelta;
                TotalSupplies = totalSuppliesDelta;
                ValidatorSet = validatorSetDelta;
            }

            /// <inheritdoc cref="IAccountDelta.UpdatedAddresses"/>
            public IImmutableSet<Address> UpdatedAddresses =>
                StateUpdatedAddresses.Union(FungibleUpdatedAddresses);

            /// <inheritdoc cref="IAccountDelta.StateUpdatedAddresses"/>
            public IImmutableSet<Address> StateUpdatedAddresses =>
                States.Keys.ToImmutableHashSet();

            /// <inheritdoc cref="IAccountDelta.States"/>
            public IImmutableDictionary<Address, IValue> States { get; }

            /// <inheritdoc cref="IAccountDelta.FungibleUpdatedAddresses"/>
            public IImmutableSet<Address> FungibleUpdatedAddresses =>
                Fungibles.Keys.Select(pair => pair.Item1).ToImmutableHashSet();

            /// <inheritdoc cref="IAccountDelta.UpdatedFungibleAssets"/>
            public IImmutableSet<(Address, Currency)> UpdatedFungibleAssets =>
                Fungibles.Keys.ToImmutableHashSet();

            /// <inheritdoc cref="IAccountDelta.Fungibles"/>
            public IImmutableDictionary<(Address, Currency), BigInteger> Fungibles { get; }

            /// <inheritdoc cref="IAccountDelta.UpdatedTotalSupplyCurrencies"/>
            public IImmutableSet<Currency> UpdatedTotalSupplyCurrencies =>
                TotalSupplies.Keys.ToImmutableHashSet();

            /// <inheritdoc cref="IAccountDelta.TotalSupplies"/>
            public IImmutableDictionary<Currency, BigInteger> TotalSupplies { get; }

            /// <inheritdoc cref="IAccountDelta.ValidatorSet"/>
            public ValidatorSet? ValidatorSet { get; }
        }

        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionContext.cs.
        /// </summary>
        private sealed class ActionContext : IActionContext
        {
            private readonly int _randomSeed;

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
                Random = new Random(randomSeed);
                _randomSeed = randomSeed;
            }

            public Address Signer { get; }

            public TxId? TxId { get; }

            public Address Miner { get; }

            public long BlockIndex { get; }

            public int BlockProtocolVersion { get; }

            public bool Rehearsal { get; }

            public IWorld PreviousState { get; }

            public IRandom Random { get; }

            public bool BlockAction => TxId is null;

            public void PutLog(string log)
            {
                // NOTE: Not implemented yet. See also Lib9c.Tests.Action.ActionContext.PutLog().
            }

            public void UseGas(long gas)
            {
            }

            public IActionContext GetUnconsumedContext() =>
                new ActionContext(
                    Signer,
                    TxId,
                    Miner,
                    BlockIndex,
                    BlockProtocolVersion,
                    PreviousState,
                    _randomSeed,
                    Rehearsal);

            public long GasUsed() => 0;

            public long GasLimit() => 0;
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

            byte[] hashedSignature;
            using (var hasher = SHA1.Create())
            {
                hashedSignature = hasher.ComputeHash(signature);
            }

            byte[] preEvaluationHashBytes = preEvaluationHash.ToByteArray();
            int seed = ActionEvaluator.GenerateRandomSeed(preEvaluationHashBytes, hashedSignature, signature, 0);

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
