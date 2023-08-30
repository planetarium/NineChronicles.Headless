﻿namespace NineChronicles.Headless.Tests.Common
{
#nullable enable

    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Numerics;
    using System.Security.Cryptography;
    using Bencodex.Types;
    using Libplanet.Action;
    using Libplanet.Action.State;
    using Libplanet.Common;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Libplanet.Types.Blocks;
    using Libplanet.Types.Consensus;

    /// <summary>
    /// A rough replica of https://github.com/planetarium/libplanet/blob/main/Libplanet/State/AccountStateDelta.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    [Pure]
    public class MockAccount : IAccount
    {
        private readonly IAccountState _baseState;

        public MockAccount(Address address)
            : this(new MockAccountState(address))
        {
        }

        public MockAccount(IAccountState baseState)
            : this(baseState, new MockAccountDelta())
        {
        }

        private MockAccount(IAccountState baseState, IAccountDelta delta)
        {
            _baseState = baseState;
            Delta = delta;
            TotalUpdatedFungibles = ImmutableDictionary<(Address, Currency), BigInteger>.Empty;
        }

        /// <inheritdoc/>
        public IAccountDelta Delta { get; private set; }

        /// <inheritdoc/>
        public Address Address => _baseState.Address;

        /// <inheritdoc/>
        public HashDigest<SHA256>? StateRootHash => _baseState.StateRootHash;

        /// <inheritdoc/>
        public BlockHash? BlockHash => _baseState.BlockHash;

        /// <inheritdoc/>
        public IImmutableSet<(Address, Currency)> TotalUpdatedFungibleAssets =>
            TotalUpdatedFungibles.Keys.ToImmutableHashSet();

        public IImmutableDictionary<(Address, Currency), BigInteger> TotalUpdatedFungibles
            { get; private set; }

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
        public IAccount SetState(Address address, IValue state) =>
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
                    $"The total supply value of the currency {currency} is not trackable " +
                    "because it is a legacy untracked currency which might have been " +
                    "established before the introduction of total supply tracking support.",
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
        public IAccount SetValidator(Validator validator)
        {
            return UpdateValidatorSet(GetValidatorSet().Update(validator));
        }

        [Pure]
        private FungibleAssetValue GetBalance(
            Address address,
            Currency currency,
            IImmutableDictionary<(Address, Currency), BigInteger> balances) =>
            balances.TryGetValue((address, currency), out BigInteger balance)
                ? FungibleAssetValue.FromRawValue(currency, balance)
                : _baseState.GetBalance(address, currency);

        [Pure]
        private MockAccount UpdateStates(
            IImmutableDictionary<Address, IValue> updatedStates) =>
            new MockAccount(
                _baseState,
                new MockAccountDelta(
                    updatedStates,
                    Delta.Fungibles,
                    Delta.TotalSupplies,
                    Delta.ValidatorSet))
            {
                TotalUpdatedFungibles = TotalUpdatedFungibles,
            };

        [Pure]
        private MockAccount UpdateFungibleAssets(
            IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets,
            IImmutableDictionary<(Address, Currency), BigInteger> totalUpdatedFungibles
        ) =>
            UpdateFungibleAssets(
                updatedFungibleAssets,
                totalUpdatedFungibles,
                Delta.TotalSupplies);

        [Pure]
        private MockAccount UpdateFungibleAssets(
            IImmutableDictionary<(Address, Currency), BigInteger> updatedFungibleAssets,
            IImmutableDictionary<(Address, Currency), BigInteger> totalUpdatedFungibles,
            IImmutableDictionary<Currency, BigInteger> updatedTotalSupply
        ) =>
            new MockAccount(
                _baseState,
                new MockAccountDelta(
                    Delta.States,
                    updatedFungibleAssets,
                    updatedTotalSupply,
                    Delta.ValidatorSet))
            {
                TotalUpdatedFungibles = totalUpdatedFungibles,
            };

        [Pure]
        private MockAccount UpdateValidatorSet(
            ValidatorSet updatedValidatorSet) =>
            new MockAccount(
                _baseState,
                new MockAccountDelta(
                    Delta.States,
                    Delta.Fungibles,
                    Delta.TotalSupplies,
                    updatedValidatorSet))
            {
                TotalUpdatedFungibles = TotalUpdatedFungibles,
            };

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
}
