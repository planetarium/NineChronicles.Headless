using System.Collections.Generic;
using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext : IAccountStateDelta
    {
        public StateContext(AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter, long chainTipIndex)
        {
            AccountStateGetter = accountStateGetter;
            AccountBalanceGetter = accountBalanceGetter;
            ChainTipIndex = chainTipIndex;
        }

        public AccountStateGetter AccountStateGetter { get; }
        public AccountBalanceGetter AccountBalanceGetter { get; }
        public long ChainTipIndex { get; }

        public IImmutableSet<Address> UpdatedAddresses => throw new System.NotImplementedException();

        public IImmutableSet<Address> StateUpdatedAddresses => throw new System.NotImplementedException();

        public IImmutableDictionary<Address, IImmutableSet<Currency>> UpdatedFungibleAssets => throw new System.NotImplementedException();

        public IValue? GetState(Address address) =>
            AccountStateGetter(new[] { address })[0];

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            AccountStateGetter(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            AccountBalanceGetter(address, currency);

        public IAccountStateDelta SetState(Address address, IValue state)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta MintAsset(Address recipient, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta TransferAsset(Address sender, Address recipient, FungibleAssetValue value, bool allowNegativeBalance = false)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta BurnAsset(Address owner, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }
    }
}
