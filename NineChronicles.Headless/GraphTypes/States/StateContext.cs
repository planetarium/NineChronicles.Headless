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
            IWorldState worldState,
            long blockIndex)
        {
            WorldState = worldState;
            BlockIndex = blockIndex;
            CurrencyFactory = new CurrencyFactory(() => worldState.GetAccount(ReservedAddresses.LegacyAccount))!;
            FungibleAssetValueFactory = new FungibleAssetValueFactory(CurrencyFactory);
        }

        public IWorldState WorldState { get; }

        public long BlockIndex { get; }

        public CurrencyFactory CurrencyFactory { get; }

        public FungibleAssetValueFactory FungibleAssetValueFactory { get; }

        public IValue? GetState(Address address, Address? account = null) => WorldState.GetAccount(account ?? ReservedAddresses.LegacyAccount).GetState(address);

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses, Address? account = null)
            => WorldState.GetAccount(account ?? ReservedAddresses.LegacyAccount).GetStates(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency, Address? account = null)
            => WorldState.GetAccount(account ?? ReservedAddresses.LegacyAccount).GetBalance(address, currency);
    }
}
