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
            long blockIndex,
            StateMemoryCache stateMemoryCache)
        {
            WorldState = worldState;
            BlockIndex = blockIndex;
            CurrencyFactory = new CurrencyFactory(() => worldState);
            FungibleAssetValueFactory = new FungibleAssetValueFactory(CurrencyFactory);
            StateMemoryCache = stateMemoryCache;
        }

        public IWorldState WorldState { get; }

        public long BlockIndex { get; }

        public CurrencyFactory CurrencyFactory { get; }

        public FungibleAssetValueFactory FungibleAssetValueFactory { get; }

        public StateMemoryCache StateMemoryCache { get; }
    }
}
