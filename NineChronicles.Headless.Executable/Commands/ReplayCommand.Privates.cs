using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Bencodex.Types;
using Cocona;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tx;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/AccountStateDeltaImpl.cs
        /// </summary>
        private class AccountStateDeltaImpl : IAccountStateDelta
        {
            public IImmutableSet<Address> UpdatedAddresses =>
                UpdatedStates.Keys.ToImmutableHashSet()
                    .Union(UpdatedFungibles
                        .Select(kv => kv.Key.Item1));

            public IImmutableSet<Address> StateUpdatedAddresses =>
                UpdatedStates.Keys.ToImmutableHashSet();

            public IImmutableDictionary<Address, IImmutableSet<Currency>>
                UpdatedFungibleAssets =>
                UpdatedFungibles
                    .GroupBy(kv => kv.Key.Item1)
                    .ToImmutableDictionary(
                        g => g.Key,
                        g =>
                            (IImmutableSet<Currency>)g
                                .Select(kv => kv.Key.Item2)
                                .ToImmutableHashSet());

            public IImmutableSet<Currency> TotalSupplyUpdatedCurrencies =>
                UpdatedTotalSupply.Keys.ToImmutableHashSet();

            protected AccountStateGetter StateGetter { get; set; }

            protected AccountBalanceGetter BalanceGetter { get; set; }

            protected TotalSupplyGetter TotalSupplyGetter { get; set; }

            protected IImmutableDictionary<Address, IValue> UpdatedStates { get; set; }

            protected IImmutableDictionary<(Address, Currency), BigInteger> UpdatedFungibles { get; set; }

            protected IImmutableDictionary<Currency, BigInteger> UpdatedTotalSupply { get; set; }

            protected Address Signer { get; set; }

            public AccountStateDeltaImpl(
                AccountStateGetter stateGetter,
                AccountBalanceGetter balanceGetter,
                TotalSupplyGetter totalSupplyGetter,
                Address signer)
            {
                StateGetter = stateGetter;
                BalanceGetter = balanceGetter;
                TotalSupplyGetter = totalSupplyGetter;
                UpdatedStates = ImmutableDictionary<Address, IValue>.Empty;
                UpdatedFungibles = ImmutableDictionary<(Address, Currency), BigInteger>.Empty;
                UpdatedTotalSupply = ImmutableDictionary<Currency, BigInteger>.Empty;
                Signer = signer;
            }

            public IValue? GetState(Address address) =>
                UpdatedStates.TryGetValue(address, out IValue? value)
                    ? value
                    : StateGetter(new[] { address })[0];

            public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
            {
                int length = addresses.Count;
                IValue?[] values = new IValue?[length];
                var notFound = new List<Address>(length);
                for (int i = 0; i < length; i++)
                {
                    Address address = addresses[i];
                    if (UpdatedStates.TryGetValue(address, out IValue? v))
                    {
                        values[i] = v;
                        continue;
                    }

                    notFound.Add(address);
                }

                IReadOnlyList<IValue?> restValues = StateGetter(notFound);
                for (int i = 0, j = 0; i < length && j < notFound.Count; i++)
                {
                    if (addresses[i].Equals(notFound[j]))
                    {
                        values[i] = restValues[j];
                        j++;
                    }
                }

                return values;
            }

            public FungibleAssetValue GetBalance(
                Address address,
                Currency currency) =>
                GetBalance(address, currency, UpdatedFungibles);

            public FungibleAssetValue GetTotalSupply(Currency currency)
            {
                if (!currency.TotalSupplyTrackable)
                {
                    // throw TotalSupplyNotTrackableException.WithDefaultMessage(currency);
                    var msg =
                        $"The total supply value of the currency {currency} is not trackable because it"
                        + " is a legacy untracked currency which might have been established before"
                        + " the introduction of total supply tracking support.";
                    throw new TotalSupplyNotTrackableException(msg, currency);
                }

                // Return dirty state if it exists.
                if (UpdatedTotalSupply.TryGetValue(currency, out BigInteger totalSupplyValue))
                {
                    return FungibleAssetValue.FromRawValue(currency, totalSupplyValue);
                }

                return TotalSupplyGetter(currency);
            }

            public IAccountStateDelta SetState(Address address, IValue state) =>
                UpdateStates(UpdatedStates.SetItem(address, state));

            public IAccountStateDelta MintAsset(Address recipient, FungibleAssetValue value)
            {
                if (value.Sign <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "The value to mint has to be greater than zero."
                    );
                }

                Currency currency = value.Currency;
                if (!currency.AllowsToMint(Signer))
                {
                    throw new CurrencyPermissionException(
                        $"The account {Signer} has no permission to mint the currency {currency}.",
                        Signer,
                        currency
                    );
                }

                FungibleAssetValue balance = GetBalance(recipient, currency);

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
                        UpdatedFungibles.SetItem((recipient, currency), (balance + value).RawValue),
                        UpdatedTotalSupply.SetItem(currency, (currentTotalSupply + value).RawValue)
                    );
                }

                return UpdateFungibleAssets(
                    UpdatedFungibles.SetItem((recipient, currency), (balance + value).RawValue)
                );
            }

            public IAccountStateDelta TransferAsset(Address sender, Address recipient, FungibleAssetValue value,
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

                IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets =
                    UpdatedFungibles
                        .SetItem((sender, currency), (senderBalance - value).RawValue);

                FungibleAssetValue recipientBalance = GetBalance(
                    recipient,
                    currency,
                    updatedFungibleAssets);

                return UpdateFungibleAssets(
                    updatedFungibleAssets
                        .SetItem((recipient, currency), (recipientBalance + value).RawValue)
                );
            }

            public IAccountStateDelta BurnAsset(Address owner, FungibleAssetValue value)
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
                if (!currency.AllowsToMint(Signer))
                {
                    msg = $"The account {Signer} has no permission to burn assets of " +
                          $"the currency {currency}.";
                    throw new CurrencyPermissionException(msg, Signer, currency);
                }

                FungibleAssetValue balance = GetBalance(owner, currency);

                if (balance < value)
                {
                    msg = $"The account {owner}'s balance of {currency} is insufficient to burn: " +
                          $"{balance} < {value}.";
                    throw new InsufficientBalanceException(msg, owner, balance);
                }

                if (currency.TotalSupplyTrackable)
                {
                    return UpdateFungibleAssets(
                        UpdatedFungibles.SetItem((owner, currency), (balance - value).RawValue),
                        UpdatedTotalSupply.SetItem(
                            currency,
                            (GetTotalSupply(currency) - value).RawValue)
                    );
                }

                return UpdateFungibleAssets(
                    UpdatedFungibles.SetItem((owner, currency), (balance - value).RawValue)
                );
            }

            public IImmutableDictionary<Address, IValue?> GetUpdatedStates() =>
                StateUpdatedAddresses.Select(address =>
                    new KeyValuePair<Address, IValue?>(
                        address,
                        GetState(address)
                    )
                ).ToImmutableDictionary();

            public IImmutableDictionary<(Address, Currency), FungibleAssetValue>
                GetUpdatedBalances() =>
                UpdatedFungibleAssets.SelectMany(kv =>
                    kv.Value.Select(currency =>
                        new KeyValuePair<(Address, Currency), FungibleAssetValue>(
                            (kv.Key, currency),
                            GetBalance(kv.Key, currency)
                        )
                    )
                ).ToImmutableDictionary();

            public IImmutableDictionary<Currency, FungibleAssetValue>
                GetUpdatedTotalSupplies() =>
                TotalSupplyUpdatedCurrencies.Select(currency =>
                        new KeyValuePair<Currency, FungibleAssetValue>(
                            currency,
                            GetTotalSupply(currency)))
                    .ToImmutableDictionary();

            public IImmutableDictionary<string, IValue?> GetUpdatedRawStates() =>
                GetUpdatedStates()
                    .Select(pair =>
                        new KeyValuePair<string, IValue?>(
                            ToStateKey(pair.Key),
                            pair.Value))
                    .Union(
                        GetUpdatedBalances().Select(pair =>
                            new KeyValuePair<string, IValue?>(
                                ToFungibleAssetKey(pair.Key),
                                (Integer)pair.Value.RawValue)))
                    .Union(
                        GetUpdatedTotalSupplies().Select(pair =>
                            new KeyValuePair<string, IValue?>(
                                ToTotalSupplyKey(pair.Key),
                                (Integer)pair.Value.RawValue))).ToImmutableDictionary();

            protected virtual FungibleAssetValue GetBalance(
                Address address,
                Currency currency,
                IImmutableDictionary<(Address, Currency), BigInteger> balances) =>
                balances.TryGetValue((address, currency), out BigInteger balance)
                    ? FungibleAssetValue.FromRawValue(currency, balance)
                    : BalanceGetter(address, currency);

            protected virtual AccountStateDeltaImpl UpdateStates(
                IImmutableDictionary<Address, IValue> updatedStates
            ) =>
                new AccountStateDeltaImpl(
                    StateGetter,
                    BalanceGetter,
                    TotalSupplyGetter,
                    Signer)
                {
                    UpdatedStates = updatedStates,
                    UpdatedFungibles = UpdatedFungibles,
                    UpdatedTotalSupply = UpdatedTotalSupply,
                };

            protected virtual AccountStateDeltaImpl UpdateFungibleAssets(
                IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets,
                IImmutableDictionary<Currency, BigInteger> updatedTotalSupply
            ) =>
                new AccountStateDeltaImpl(
                    StateGetter,
                    BalanceGetter,
                    TotalSupplyGetter,
                    Signer)
                {
                    UpdatedStates = UpdatedStates,
                    UpdatedFungibles = updatedFungibleAssets,
                    UpdatedTotalSupply = updatedTotalSupply,
                };

            protected virtual AccountStateDeltaImpl UpdateFungibleAssets(
                IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets
            ) =>
                UpdateFungibleAssets(updatedFungibleAssets, UpdatedTotalSupply);

            public static string ToStateKey(Address address) =>
                address.ToHex().ToLowerInvariant();

            public static string ToFungibleAssetKey(Address address, Currency currency) =>
                "_" + address.ToHex().ToLowerInvariant() +
                "_" + ByteUtil.Hex(currency.Hash.ByteArray).ToLowerInvariant();

            public static string ToFungibleAssetKey((Address, Currency) pair) =>
                ToFungibleAssetKey(pair.Item1, pair.Item2);

            public static string ToTotalSupplyKey(Currency currency) =>
                "__" + ByteUtil.Hex(currency.Hash.ByteArray).ToLowerInvariant();
        }

        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionContext.cs
        /// </summary>
        private sealed class ActionContext : IActionContext
        {
            private readonly int _randomSeed;
            private readonly ITrie? _previousBlockStatesTrie;
            private readonly Predicate<Currency>? _nativeTokenPredicate;
            private HashDigest<SHA256>? _previousStateRootHash;

            public ActionContext(
                BlockHash? genesisHash,
                Address signer,
                TxId? txid,
                Address miner,
                long blockIndex,
                IAccountStateDelta previousStates,
                int randomSeed,
                bool rehearsal = false,
                ITrie? previousBlockStatesTrie = null,
                bool blockAction = false,
                Predicate<Currency>? nativeTokenPredicate = null)
            {
                GenesisHash = genesisHash;
                Signer = signer;
                TxId = txid;
                Miner = miner;
                BlockIndex = blockIndex;
                Rehearsal = rehearsal;
                PreviousStates = previousStates;
                Random = new Random(randomSeed);
                _randomSeed = randomSeed;
                _previousBlockStatesTrie = previousBlockStatesTrie;
                BlockAction = blockAction;
                _nativeTokenPredicate = nativeTokenPredicate;
            }

            public BlockHash? GenesisHash { get; }

            public Address Signer { get; }

            public TxId? TxId { get; }

            public Address Miner { get; }

            public long BlockIndex { get; }

            public bool Rehearsal { get; }

            public IAccountStateDelta PreviousStates { get; }

            public IRandom Random { get; }

            public HashDigest<SHA256>? PreviousStateRootHash =>
                _previousStateRootHash ??= _previousBlockStatesTrie is null
                    ? null
                    : Set(
                            _previousBlockStatesTrie!,
                            GetUpdatedRawStates(PreviousStates))
                        .Commit()
                        .Hash;

            public bool BlockAction { get; }

            public void PutLog(string log)
            {
                // NOTE: Not implemented yet. See also Lib9c.Tests.Action.ActionContext.PutLog().
            }

            public bool IsNativeToken(Currency currency) =>
                _nativeTokenPredicate is { } && _nativeTokenPredicate(currency);

            public IActionContext GetUnconsumedContext() =>
                new ActionContext(
                    GenesisHash,
                    Signer,
                    TxId,
                    Miner,
                    BlockIndex,
                    PreviousStates,
                    _randomSeed,
                    Rehearsal,
                    _previousBlockStatesTrie,
                    BlockAction,
                    _nativeTokenPredicate);

            private IImmutableDictionary<string, IValue?> GetUpdatedRawStates(
                IAccountStateDelta delta)
            {
                if (delta is not AccountStateDeltaImpl impl)
                {
                    return ImmutableDictionary<string, IValue?>.Empty;
                }

                return impl.GetUpdatedStates()
                    .Select(pair =>
                        new KeyValuePair<string, IValue?>(
                            AccountStateDeltaImpl.ToStateKey(pair.Key),
                            pair.Value))
                    .Union(
                        impl.GetUpdatedBalances().Select(pair =>
                            new KeyValuePair<string, IValue?>(
                                AccountStateDeltaImpl.ToFungibleAssetKey(pair.Key),
                                (Integer)pair.Value.RawValue)))
                    .Union(
                        impl.GetUpdatedTotalSupplies().Select(pair =>
                            new KeyValuePair<string, IValue?>(
                                AccountStateDeltaImpl.ToTotalSupplyKey(pair.Key),
                                (Integer)pair.Value.RawValue))).ToImmutableDictionary();
            }

            private ITrie Set(ITrie trie, IEnumerable<KeyValuePair<string, IValue?>> pairs)
                => Set(
                    trie,
                    pairs.Select(pair =>
                        new KeyValuePair<KeyBytes, IValue?>(
                            StateStoreExtensions.EncodeKey(pair.Key),
                            pair.Value
                        )
                    )
                );

            private ITrie Set(ITrie trie, IEnumerable<KeyValuePair<KeyBytes, IValue?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    if (pair.Value is { } v)
                    {
                        trie = trie.Set(pair.Key, v);
                    }
                    else
                    {
                        throw new NotSupportedException(
                            "Unsetting states is not supported yet.  " +
                            "See also: https://github.com/planetarium/libplanet/issues/1383"
                        );
                    }
                }

                return trie;
            }
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
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionEvaluator.cs#L286
        /// </summary>
        private static IEnumerable<ActionEvaluation> EvaluateActions(
            BlockHash? genesisHash,
            ImmutableArray<byte> preEvaluationHash,
            long blockIndex,
            TxId? txid,
            IAccountStateDelta previousStates,
            Address miner,
            Address signer,
            byte[] signature,
            IImmutableList<IAction> actions,
            Predicate<Currency> nativeTokenPredicate,
            bool rehearsal = false,
            ITrie? previousBlockStatesTrie = null,
            bool blockAction = false,
            ILogger? logger = null)
        {
            ActionContext CreateActionContext(IAccountStateDelta prevStates, int randomSeed)
            {
                return new ActionContext(
                    genesisHash: genesisHash,
                    signer: signer,
                    txid: txid,
                    miner: miner,
                    blockIndex: blockIndex,
                    previousStates: prevStates,
                    randomSeed: randomSeed,
                    rehearsal: rehearsal,
                    previousBlockStatesTrie: previousBlockStatesTrie,
                    blockAction: blockAction,
                    nativeTokenPredicate: nativeTokenPredicate);
            }

            byte[] hashedSignature;
            using (var hasher = SHA1.Create())
            {
                hashedSignature = hasher.ComputeHash(signature);
            }

            byte[] preEvaluationHashBytes = preEvaluationHash.ToBuilder().ToArray();
            int seed = ActionEvaluator<NCAction>.GenerateRandomSeed(
                preEvaluationHashBytes,
                hashedSignature,
                signature,
                0);

            IAccountStateDelta states = previousStates;
            foreach (IAction action in actions)
            {
                Exception? exc = null;
                ActionContext context = CreateActionContext(states, seed);
                IAccountStateDelta nextStates = context.PreviousStates;
                try
                {
                    DateTimeOffset actionExecutionStarted = DateTimeOffset.Now;
                    nextStates = action.Execute(context);
                    TimeSpan spent = DateTimeOffset.Now - actionExecutionStarted;
                    logger?.Verbose($"{action} execution spent {spent.TotalMilliseconds} ms.");
                }
                catch (OutOfMemoryException e)
                {
                    // Because OutOfMemory is thrown non-deterministically depending on the state
                    // of the node, we should throw without further handling.
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution.";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash));
                    throw;
                }
                catch (Exception e)
                {
                    if (rehearsal)
                    {
                        var message =
                            $"The action {action} threw an exception during its " +
                            "rehearsal.  It is probably because the logic of the " +
                            $"action {action} is not enough generic so that it " +
                            "can cover every case including rehearsal mode.\n" +
                            "The IActionContext.Rehearsal property also might be " +
                            "useful to make the action can deal with the case of " +
                            "rehearsal mode.\n" +
                            "See also this exception's InnerException property.";
                        exc = new UnexpectedlyTerminatedActionException(
                            message, null, null, null, null, action, e);
                    }
                    else
                    {
                        var stateRootHash = context.PreviousStateRootHash;
                        var message =
                            "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                            "pre-evaluation hash {PreEvaluationHash} and previous " +
                            "state root hash {StateRootHash} threw an exception " +
                            "during execution.";
                        logger?.Error(
                            e,
                            message,
                            action,
                            txid,
                            blockIndex,
                            ByteUtil.Hex(preEvaluationHash),
                            stateRootHash);
                        var innerMessage =
                            $"The action {action} (block #{blockIndex}, " +
                            $"pre-evaluation hash {ByteUtil.Hex(preEvaluationHash)}, tx {txid}, " +
                            $"previous state root hash {stateRootHash}) threw " +
                            "an exception during execution.  " +
                            "See also this exception's InnerException property.";
                        logger?.Error(
                            "{Message}\nInnerException: {ExcMessage}", innerMessage, e.Message);
                        exc = new UnexpectedlyTerminatedActionException(
                            innerMessage,
                            preEvaluationHash,
                            blockIndex,
                            txid,
                            stateRootHash,
                            action,
                            e);
                    }
                }

                // As IActionContext.Random is stateful, we cannot reuse
                // the context which is once consumed by Execute().
                ActionContext equivalentContext = CreateActionContext(states, seed);

                yield return new ActionEvaluation(
                    action: action,
                    inputContext: equivalentContext,
                    outputStates: nextStates,
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
