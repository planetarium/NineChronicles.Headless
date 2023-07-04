#nullable enable

using System.Collections.Generic;
using Bencodex.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.State;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext
    {
        public StateContext(
            AccountStateGetter accountStateGetter,
            AccountBalanceGetter accountBalanceGetter,
            long blockIndex)
        {
            AccountStateGetter = accountStateGetter;
            AccountBalanceGetter = accountBalanceGetter;
            BlockIndex = blockIndex;
            CurrencyFactory = new CurrencyFactory(accountStateGetter);
            FungibleAssetValueFactory = new FungibleAssetValueFactory(CurrencyFactory);
        }

        public AccountStateGetter AccountStateGetter { get; }
        public AccountBalanceGetter AccountBalanceGetter { get; }
        public long BlockIndex { get; }

        public CurrencyFactory CurrencyFactory { get; }

        public FungibleAssetValueFactory FungibleAssetValueFactory { get; }

        public IValue? GetState(Address address) =>
            AccountStateGetter(new[] { address })[0];

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            AccountStateGetter(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            AccountBalanceGetter(address, currency);
    }
}
