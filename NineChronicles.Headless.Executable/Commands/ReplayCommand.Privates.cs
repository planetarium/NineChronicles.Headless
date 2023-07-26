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
using Libplanet.Types.Tx;
using Serilog;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/State/AccountStateDelta.cs.
        /// </summary>
        [Pure]
        private sealed class AccountStateDelta : IAccountStateDelta
        {
            private readonly IAccountState _baseState;

            private AccountStateDelta(IAccountState baseState)
                : this(baseState, new AccountDelta())
            {
            }

            private AccountStateDelta(IAccountState baseState, IAccountDelta delta)
            {
                _baseState = baseState;
                Delta = delta;
                TotalUpdatedFungibles = ImmutableDictionary<(Address, Currency), BigInteger>.Empty;
            }

            /// <inheritdoc/>
            public IAccountDelta Delta { get; private set; }

            /// <inheritdoc/>
            public IImmutableSet<(Address, Currency)> TotalUpdatedFungibleAssets =>
                TotalUpdatedFungibles.Keys.ToImmutableHashSet();

            public IImmutableDictionary<(Address, Currency), BigInteger> TotalUpdatedFungibles { get; private set; }

            /// <inheritdoc/>
            [Pure]
            public IValue? GetState(Address address)
            {
                IValue? state = GetStates(new[] { address })[0];
                return state;
            }

            /// <inheritdoc cref="IAccountState.GetStates(IReadOnlyList{Address})"/>
            [Pure]
            public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
            {
                int length = addresses.Count;
                IValue?[] values = new IValue?[length];
                var notFoundIndices = new List<int>(length);
                for (int i = 0; i < length; i++)
                {
                    Address address = addresses[i];
                    if (Delta.States.TryGetValue(address, out IValue? updatedValue))
                    {
                        values[i] = updatedValue;
                    }
                    else
                    {
                        notFoundIndices.Add(i);
                    }
                }

                if (notFoundIndices.Count > 0)
                {
                    IReadOnlyList<IValue?> restValues = _baseState.GetStates(
                        notFoundIndices.Select(index => addresses[index]).ToArray());
                    foreach ((var v, var i) in notFoundIndices.Select((v, i) => (v, i)))
                    {
                        values[v] = restValues[i];
                    }
                }

                return values;
            }

            /// <inheritdoc/>
            [Pure]
            public IAccountStateDelta SetState(Address address, IValue state) =>
                UpdateStates(Delta.States.SetItem(address, state));

            /// <inheritdoc/>
            [Pure]
            public FungibleAssetValue GetBalance(Address address, Currency currency) =>
                GetBalance(address, currency, Delta.Fungibles);

            /// <inheritdoc/>
            [Pure]
            public FungibleAssetValue GetTotalSupply(Currency currency)
            {
                if (!currency.TotalSupplyTrackable)
                {
                    throw new TotalSupplyNotTrackableException(
                        $"Given currency {currency} is not trackable for its total supply",
                        currency);
                }

                // Return dirty state if it exists.
                if (Delta.TotalSupplies.TryGetValue(currency, out BigInteger totalSupplyValue))
                {
                    return FungibleAssetValue.FromRawValue(currency, totalSupplyValue);
                }

                return _baseState.GetTotalSupply(currency);
            }

            /// <inheritdoc/>
            [Pure]
            public ValidatorSet GetValidatorSet() =>
                Delta.ValidatorSet ?? _baseState.GetValidatorSet();

            /// <inheritdoc/>
            [Pure]
            public IAccountStateDelta MintAsset(
                IActionContext context, Address recipient, FungibleAssetValue value)
            {
                if (value.Sign <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "The value to mint has to be greater than zero."
                    );
                }

                Currency currency = value.Currency;
                if (!currency.AllowsToMint(context.Signer))
                {
                    throw new CurrencyPermissionException(
                        $"The account {context.Signer} has no permission to mint currency {currency}.",
                        context.Signer,
                        currency
                    );
                }

                FungibleAssetValue balance = GetBalance(recipient, currency);
                (Address, Currency) assetKey = (recipient, currency);
                BigInteger rawBalance = (balance + value).RawValue;

                if (currency.TotalSupplyTrackable)
                {
                    var currentTotalSupply = GetTotalSupply(currency);
                    if (currency.MaximumSupply < currentTotalSupply + value)
                    {
                        var msg = $"The amount {value} attempted to be minted added to the current"
                                + $" total supply of {currentTotalSupply} exceeds the"
                                + $" maximum allowed supply of {currency.MaximumSupply}.";
                        throw new SupplyOverflowException(msg, value);
                    }

                    return UpdateFungibleAssets(
                        Delta.Fungibles.SetItem(assetKey, rawBalance),
                        TotalUpdatedFungibles.SetItem(assetKey, rawBalance),
                        Delta.TotalSupplies.SetItem(currency, (currentTotalSupply + value).RawValue)
                    );
                }

                return UpdateFungibleAssets(
                    Delta.Fungibles.SetItem(assetKey, rawBalance),
                    TotalUpdatedFungibles.SetItem(assetKey, rawBalance)
                );
            }

            /// <inheritdoc/>
            [Pure]
            public IAccountStateDelta TransferAsset(
                IActionContext context,
                Address sender,
                Address recipient,
                FungibleAssetValue value,
                bool allowNegativeBalance = false) => context.BlockProtocolVersion > 0
                    ? TransferAssetV1(sender, recipient, value, allowNegativeBalance)
                    : TransferAssetV0(sender, recipient, value, allowNegativeBalance);

            /// <inheritdoc/>
            [Pure]
            public IAccountStateDelta BurnAsset(
                IActionContext context, Address owner, FungibleAssetValue value)
            {
                string msg;

                if (value.Sign <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "The value to burn has to be greater than zero."
                    );
                }

                Currency currency = value.Currency;
                if (!currency.AllowsToMint(context.Signer))
                {
                    msg = $"The account {context.Signer} has no permission to burn assets of " +
                        $"the currency {currency}.";
                    throw new CurrencyPermissionException(msg, context.Signer, currency);
                }

                FungibleAssetValue balance = GetBalance(owner, currency);

                if (balance < value)
                {
                    msg = $"The account {owner}'s balance of {currency} is insufficient to burn: " +
                        $"{balance} < {value}.";
                    throw new InsufficientBalanceException(msg, owner, balance);
                }

                (Address, Currency) assetKey = (owner, currency);
                BigInteger rawBalance = (balance - value).RawValue;
                if (currency.TotalSupplyTrackable)
                {
                    return UpdateFungibleAssets(
                        Delta.Fungibles.SetItem(assetKey, rawBalance),
                        TotalUpdatedFungibles.SetItem(assetKey, rawBalance),
                        Delta.TotalSupplies.SetItem(
                            currency,
                            (GetTotalSupply(currency) - value).RawValue)
                    );
                }

                return UpdateFungibleAssets(
                    Delta.Fungibles.SetItem(assetKey, rawBalance),
                    TotalUpdatedFungibles.SetItem(assetKey, rawBalance)
                );
            }

            /// <inheritdoc/>
            [Pure]
            public IAccountStateDelta SetValidator(Validator validator)
            {
                return UpdateValidatorSet(GetValidatorSet().Update(validator));
            }

            /// <summary>
            /// Creates a null state delta from given <paramref name="previousState"/>.
            /// </summary>
            /// <param name="previousState">The previous <see cref="IAccountState"/> to use as
            /// a basis.</param>
            /// <returns>A null state delta created from <paramref name="previousState"/>.
            /// </returns>
            internal static IAccountStateDelta Create(IAccountState previousState) =>
                new AccountStateDelta(previousState);

            /// <summary>
            /// Creates a null state delta while inheriting <paramref name="stateDelta"/>s
            /// total updated fungibles.
            /// </summary>
            /// <param name="stateDelta">The previous <see cref="IAccountStateDelta"/> to use.</param>
            /// <returns>A null state delta that is of the same type as <paramref name="stateDelta"/>.
            /// </returns>
            /// <exception cref="ArgumentException">Thrown if given <paramref name="stateDelta"/>
            /// is not <see cref="AccountStateDelta"/>.
            /// </exception>
            /// <remarks>
            /// This inherits <paramref name="stateDelta"/>'s
            /// <see cref="IAccountStateDelta.TotalUpdatedFungibleAssets"/>.
            /// </remarks>
            internal static IAccountStateDelta Flush(IAccountStateDelta stateDelta) =>
                stateDelta is AccountStateDelta impl
                    ? new AccountStateDelta(stateDelta)
                    {
                        TotalUpdatedFungibles = impl.TotalUpdatedFungibles,
                    }
                    : throw new ArgumentException(
                        $"Unknown type for {nameof(stateDelta)}: {stateDelta.GetType()}");

            [Pure]
            private FungibleAssetValue GetBalance(
                Address address,
                Currency currency,
                IImmutableDictionary<(Address, Currency), BigInteger> balances) =>
                balances.TryGetValue((address, currency), out BigInteger balance)
                    ? FungibleAssetValue.FromRawValue(currency, balance)
                    : _baseState.GetBalance(address, currency);

            [Pure]
            private AccountStateDelta UpdateStates(
                IImmutableDictionary<Address, IValue> updatedStates) =>
                new AccountStateDelta(
                    _baseState,
                    new AccountDelta(
                        updatedStates,
                        Delta.Fungibles,
                        Delta.TotalSupplies,
                        Delta.ValidatorSet))
                {
                    TotalUpdatedFungibles = TotalUpdatedFungibles,
                };

            [Pure]
            private AccountStateDelta UpdateFungibleAssets(
                IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets,
                IImmutableDictionary<(Address, Currency), BigInteger> totalUpdatedFungibles
            ) =>
                UpdateFungibleAssets(
                    updatedFungibleAssets,
                    totalUpdatedFungibles,
                    Delta.TotalSupplies);

            [Pure]
            private AccountStateDelta UpdateFungibleAssets(
                IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets,
                IImmutableDictionary<(Address, Currency), BigInteger> totalUpdatedFungibles,
                IImmutableDictionary<Currency, BigInteger> updatedTotalSupply
            ) =>
                new AccountStateDelta(
                    _baseState,
                    new AccountDelta(
                        Delta.States,
                        updatedFungibleAssets,
                        updatedTotalSupply,
                        Delta.ValidatorSet))
                {
                    TotalUpdatedFungibles = totalUpdatedFungibles,
                };

            [Pure]
            private AccountStateDelta UpdateValidatorSet(
                ValidatorSet updatedValidatorSet) =>
                new AccountStateDelta(
                    _baseState,
                    new AccountDelta(
                        Delta.States,
                        Delta.Fungibles,
                        Delta.TotalSupplies,
                        updatedValidatorSet))
                {
                    TotalUpdatedFungibles = TotalUpdatedFungibles,
                };

            [Pure]
            private IAccountStateDelta TransferAssetV0(
                Address sender,
                Address recipient,
                FungibleAssetValue value,
                bool allowNegativeBalance = false)
            {
                if (value.Sign <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "The value to transfer has to be greater than zero."
                    );
                }

                Currency currency = value.Currency;
                FungibleAssetValue senderBalance = GetBalance(sender, currency);
                FungibleAssetValue recipientBalance = GetBalance(recipient, currency);

                if (!allowNegativeBalance && senderBalance < value)
                {
                    var msg = $"The account {sender}'s balance of {currency} is insufficient to " +
                            $"transfer: {senderBalance} < {value}.";
                    throw new InsufficientBalanceException(msg, sender, senderBalance);
                }

                return UpdateFungibleAssets(
                    Delta.Fungibles
                        .SetItem((sender, currency), (senderBalance - value).RawValue)
                        .SetItem((recipient, currency), (recipientBalance + value).RawValue),
                    TotalUpdatedFungibles
                        .SetItem((sender, currency), (senderBalance - value).RawValue)
                        .SetItem((recipient, currency), (recipientBalance + value).RawValue)
                );
            }

            [Pure]
            private IAccountStateDelta TransferAssetV1(
                Address sender,
                Address recipient,
                FungibleAssetValue value,
                bool allowNegativeBalance = false)
            {
                if (value.Sign <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "The value to transfer has to be greater than zero."
                    );
                }

                Currency currency = value.Currency;
                FungibleAssetValue senderBalance = GetBalance(sender, currency);

                if (!allowNegativeBalance && senderBalance < value)
                {
                    var msg = $"The account {sender}'s balance of {currency} is insufficient to " +
                            $"transfer: {senderBalance} < {value}.";
                    throw new InsufficientBalanceException(msg, sender, senderBalance);
                }

                (Address, Currency) senderAssetKey = (sender, currency);
                BigInteger senderRawBalance = (senderBalance - value).RawValue;

                IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets =
                    Delta.Fungibles.SetItem(senderAssetKey, senderRawBalance);
                IImmutableDictionary<(Address, Currency), BigInteger> totalUpdatedFungibles =
                    TotalUpdatedFungibles.SetItem(senderAssetKey, senderRawBalance);

                FungibleAssetValue recipientBalance = GetBalance(
                    recipient,
                    currency,
                    updatedFungibleAssets);
                (Address, Currency) recipientAssetKey = (recipient, currency);
                BigInteger recipientRawBalance = (recipientBalance + value).RawValue;

                return UpdateFungibleAssets(
                    updatedFungibleAssets.SetItem(recipientAssetKey, recipientRawBalance),
                    totalUpdatedFungibles.SetItem(recipientAssetKey, recipientRawBalance)
                );
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
                IAccountStateDelta previousState,
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

            public IAccountStateDelta PreviousState { get; }

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
            IAccountStateDelta previousStates,
            Address miner,
            Address signer,
            byte[] signature,
            IImmutableList<IAction> actions,
            ILogger? logger = null)
        {
            ActionContext CreateActionContext(
                IAccountStateDelta prevState,
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

            IAccountStateDelta states = previousStates;
            foreach (IAction action in actions)
            {
                Exception? exc = null;
                IAccountStateDelta nextStates = states;
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
