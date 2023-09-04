#nullable enable

using Libplanet.Crypto;
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
            CurrencyFactory = new CurrencyFactory(() => worldState);
            FungibleAssetValueFactory = new FungibleAssetValueFactory(CurrencyFactory);
        }

        public IWorldState WorldState { get; }

        public long BlockIndex { get; }

        public CurrencyFactory CurrencyFactory { get; }

        public FungibleAssetValueFactory FungibleAssetValueFactory { get; }

        public IAccount GetAccount(Address address) => WorldState.GetAccount(address);
    }
}
