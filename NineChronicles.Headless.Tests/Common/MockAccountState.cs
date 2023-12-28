using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using static NineChronicles.Headless.Tests.Common.KeyConverters;

namespace NineChronicles.Headless.Tests.Common
{
    /// <summary>
    /// A rough replica of https://github.com/planetarium/libplanet/blob/main/Libplanet.Action/State/AccountState.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    public class MockAccountState : IAccountState
    {
        public MockAccountState() : this(ImmutableDictionary<KeyBytes, IValue>.Empty)
        {
        }

        public MockAccountState(IImmutableDictionary<KeyBytes, IValue> mockTrie)
        {
            MockTrie = mockTrie;
        }

        /// <inheritdoc cref="IAccountState.Trie"/>
        public ITrie Trie => throw new NotSupportedException();

        /// <inheritdoc cref="IAccountState.Trie"/>
        public IImmutableDictionary<KeyBytes, IValue> MockTrie { get; }

        /// <inheritdoc cref="IAccountState.GetState"/>
        public IValue GetState(Address address) => MockTrie[ToStateKey(address)];

        /// <inheritdoc cref="IAccountState.GetStates"/>
        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            addresses.Select(address => GetState(address)).ToList();

        /// <inheritdoc cref="IAccountState.GetBalance"/>
        public FungibleAssetValue GetBalance(Address address, Currency currency)
        {
            IValue? value = MockTrie[ToFungibleAssetKey(address, currency)];
            return value is Integer i
                ? FungibleAssetValue.FromRawValue(currency, i)
                : currency * 0;
        }

        /// <inheritdoc cref="IAccountState.GetTotalSupply"/>
        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            if (!currency.TotalSupplyTrackable)
            {
                throw TotalSupplyNotTrackableException.WithDefaultMessage(currency);
            }

            IValue? value = MockTrie[ToTotalSupplyKey(currency)];
            return value is Integer i
                ? FungibleAssetValue.FromRawValue(currency, i)
                : currency * 0;
        }

        /// <inheritdoc cref="IAccountState.GetValidatorSet"/>
        public ValidatorSet GetValidatorSet()
        {
            IValue? value = MockTrie[ValidatorSetKey];
            return value is List list
                ? new ValidatorSet(list)
                : new ValidatorSet();
        }

        // Methods used in unit tests
        public MockAccountState SetState(Address address, IValue state) =>
            new MockAccountState(MockTrie.SetItem(ToStateKey(address), state));

        public MockAccountState SetBalance(Address address, FungibleAssetValue amount) =>
            SetBalance((address, amount.Currency), amount.RawValue);

        public MockAccountState SetBalance(Address address, Currency currency, BigInteger rawAmount) =>
            SetBalance((address, currency), rawAmount);

        public MockAccountState SetBalance((Address Address, Currency Currency) pair, BigInteger rawAmount) =>
            new MockAccountState(MockTrie.SetItem(ToFungibleAssetKey(pair), (Integer)rawAmount));

        public MockAccountState AddBalance(Address address, FungibleAssetValue amount) =>
            AddBalance((address, amount.Currency), amount.RawValue);

        public MockAccountState AddBalance(Address address, Currency currency, BigInteger rawAmount) =>
            AddBalance((address, currency), rawAmount);

        public MockAccountState AddBalance((Address Address, Currency Currency) pair, BigInteger rawAmount)
        {
            var amount = GetBalance(pair.Address, pair.Currency).RawValue + rawAmount;
            return SetBalance(pair, amount);
        }

        public MockAccountState SubtractBalance(Address address, FungibleAssetValue amount) =>
            SubtractBalance((address, amount.Currency), amount.RawValue);

        public MockAccountState SubtractBalance(Address address, Currency currency, BigInteger rawAmount) =>
            SubtractBalance((address, currency), rawAmount);

        public MockAccountState SubtractBalance((Address Address, Currency Currency) pair, BigInteger rawAmount) 
        {
            var amount = GetBalance(pair.Address, pair.Currency).RawValue - rawAmount;
            return SetBalance(pair, amount);
        }

        public MockAccountState TransferBalance(Address sender, Address recipient, FungibleAssetValue amount) =>
            TransferBalance(sender, recipient, amount.Currency, amount.RawValue);

        public MockAccountState TransferBalance(Address sender, Address recipient, Currency currency, BigInteger rawAmount) =>
            SubtractBalance(sender, currency, rawAmount).AddBalance(recipient, currency, rawAmount);

        public MockAccountState SetTotalSupply(FungibleAssetValue amount) =>
            SetTotalSupply(amount.Currency, amount.RawValue);

        public MockAccountState SetTotalSupply(Currency currency, BigInteger rawAmount) =>
            currency.TotalSupplyTrackable
                ? !(currency.MaximumSupply is { } maximumSupply) || rawAmount <= maximumSupply.RawValue
                    ? new MockAccountState(MockTrie.SetItem(ToTotalSupplyKey(currency), (Integer)rawAmount))
                    : throw new ArgumentException(
                        $"Given {currency}'s total supply is capped at {maximumSupply.RawValue} and " +
                        $"cannot be set to {rawAmount}.")
                : throw new ArgumentException(
                    $"Given {currency} is not trackable.");

        public MockAccountState AddTotalSupply(FungibleAssetValue amount) =>
            AddTotalSupply(amount.Currency, amount.RawValue);

        public MockAccountState AddTotalSupply(Currency currency, BigInteger rawAmount)
        {
            var amount = GetTotalSupply(currency).RawValue + rawAmount;
            return SetTotalSupply(currency, amount);
        }

        public MockAccountState SubtractTotalSupply(FungibleAssetValue amount) =>
            SubtractTotalSupply(amount.Currency, amount.RawValue);

        public MockAccountState SubtractTotalSupply(Currency currency, BigInteger rawAmount)
        {
            var amount = GetTotalSupply(currency).RawValue - rawAmount;
            return SetTotalSupply(currency, amount);
        }

        public MockAccountState SetValidator(Validator validator) =>
            new MockAccountState(MockTrie.SetItem(ValidatorSetKey, GetValidatorSet().Update(validator).Bencoded));
    }
}
