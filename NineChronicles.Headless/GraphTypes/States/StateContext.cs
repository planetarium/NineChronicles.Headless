#nullable enable

using System.Collections.Generic;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Action.State;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext
    {
        public StateContext(
            IAccountState accountState,
            long blockIndex)
        {
            AccountState = accountState;
            BlockIndex = blockIndex;
            CurrencyFactory = new CurrencyFactory(() => accountState);
            FungibleAssetValueFactory = new FungibleAssetValueFactory(CurrencyFactory);
        }

        public IAccountState AccountState { get; }

        public long BlockIndex { get; }

        public CurrencyFactory CurrencyFactory { get; }

        public FungibleAssetValueFactory FungibleAssetValueFactory { get; }

        public IValue? GetState(Address address) => AccountState.GetState(address);

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) => AccountState.GetStates(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) => AccountState.GetBalance(address, currency);
    }
}
