namespace NineChronicles.Headless.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Numerics;
    using Bencodex.Types;
    using Libplanet.Action;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Store.Trie;
    using Libplanet.Types.Assets;
    using Libplanet.Types.Consensus;
    using static KeyConverters;

    /// <summary>
    /// A rough replica of https://github.com/planetarium/libplanet/blob/main/Libplanet.Action/State/Account.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    [Pure]
    public class MockAccount : IAccount
    {
        private readonly MockAccountState _state;

        public MockAccount(MockAccountState state)
            : this(state, ImmutableHashSet<(Address, Currency)>.Empty)
        {
        }

        public MockAccount(
            MockAccountState state,
            IImmutableSet<(Address, Currency)> totalUpdatedFungibleAssets)
        {
            _state = state;
            TotalUpdatedFungibleAssets = totalUpdatedFungibleAssets;
        }

        /// <inheritdoc/>
        public ITrie Trie => throw new NotSupportedException();

        public IImmutableDictionary<KeyBytes, IValue> MockTrie => _state.MockTrie;

        /// <inheritdoc/>
        public IImmutableSet<(Address, Currency)> TotalUpdatedFungibleAssets { get; }

        /// <inheritdoc/>
        [Pure]
        public IValue? GetState(Address address) => _state.GetState(address);

        /// <inheritdoc cref="IAccountState.GetStates(IReadOnlyList{Address})"/>
        [Pure]
        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            _state.GetStates(addresses);

        /// <inheritdoc/>
        [Pure]
        public IAccount SetState(Address address, IValue state) => UpdateState(address, state);

        /// <inheritdoc/>
        [Pure]
        public IAccount RemoveState(Address address) => UpdateState(address);

        /// <inheritdoc/>
        [Pure]
        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            _state.GetBalance(address, currency);

        /// <inheritdoc/>
        [Pure]
        public FungibleAssetValue GetTotalSupply(Currency currency) =>
            _state.GetTotalSupply(currency);

        /// <inheritdoc/>
        [Pure]
        public ValidatorSet GetValidatorSet() => _state.GetValidatorSet();

        /// <inheritdoc/>
        [Pure]
        public IAccount MintAsset(
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
                    recipient,
                    currency,
                    rawBalance,
                    (currentTotalSupply + value).RawValue);
            }
            else
            {
                return UpdateFungibleAssets(recipient, currency, rawBalance);
            }
        }

        /// <inheritdoc/>
        [Pure]
        public IAccount TransferAsset(
            IActionContext context,
            Address sender,
            Address recipient,
            FungibleAssetValue value,
            bool allowNegativeBalance = false) => context.BlockProtocolVersion > 0
                ? TransferAssetV1(sender, recipient, value, allowNegativeBalance)
                : TransferAssetV0(sender, recipient, value, allowNegativeBalance);

        /// <inheritdoc/>
        [Pure]
        public IAccount BurnAsset(
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

            BigInteger rawBalance = (balance - value).RawValue;
            if (currency.TotalSupplyTrackable)
            {
                var currentTotalSupply = GetTotalSupply(currency);
                return UpdateFungibleAssets(
                    owner,
                    currency,
                    rawBalance,
                    (currentTotalSupply - value).RawValue);
            }
            else
            {
                return UpdateFungibleAssets(owner, currency, rawBalance);
            }
        }

        /// <inheritdoc/>
        [Pure]
        public IAccount SetValidator(Validator validator) =>
            UpdateValidatorSet(GetValidatorSet().Update(validator));

        public IAccount SetValidatorSet(ValidatorSet validatorSet) =>
            UpdateValidatorSet(validatorSet);

        [Pure]
        private MockAccount UpdateState(
            Address address,
            IValue value) =>
            new MockAccount(
                new MockAccountState(
                    MockTrie.Add(ToStateKey(address), value)),
                TotalUpdatedFungibleAssets);

        [Pure]
        private MockAccount UpdateState(
            Address address) =>
            new MockAccount(
                new MockAccountState(
                    MockTrie.Remove(ToStateKey(address))),
                TotalUpdatedFungibleAssets);

        [Pure]
        private MockAccount UpdateFungibleAssets(
            Address address,
            Currency currency,
            BigInteger amount,
            BigInteger? supplyAmount = null) => supplyAmount is { } sa
            ? new MockAccount(
                new MockAccountState(
                    MockTrie
                        .Add(ToFungibleAssetKey(address, currency), new Integer(amount))
                        .Add(ToTotalSupplyKey(currency), new Integer(sa))),
                TotalUpdatedFungibleAssets.Add((address, currency)))
            : new MockAccount(
                new MockAccountState(
                    MockTrie.Add(ToFungibleAssetKey(address, currency), new Integer(amount))),
                TotalUpdatedFungibleAssets.Add((address, currency)));

        [Pure]
        private MockAccount UpdateValidatorSet(ValidatorSet validatorSet) =>
            new MockAccount(
                new MockAccountState(
                    MockTrie.Add(ValidatorSetKey, validatorSet.Bencoded)),
                TotalUpdatedFungibleAssets);

        [Pure]
        private IAccount TransferAssetV0(
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

            return UpdateFungibleAssets(sender, currency, (senderBalance - value).RawValue)
                .UpdateFungibleAssets(recipient, currency, (recipientBalance + value).RawValue);
        }

        [Pure]
        private IAccount TransferAssetV1(
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

            BigInteger senderRawBalance = (senderBalance - value).RawValue;
            MockAccount intermediate = UpdateFungibleAssets(sender, currency, senderRawBalance);
            FungibleAssetValue recipientBalance = intermediate.GetBalance(recipient, currency);
            BigInteger recipientRawBalance = (recipientBalance + value).RawValue;

            return intermediate.UpdateFungibleAssets(recipient, currency, recipientRawBalance);
        }
    }
}
