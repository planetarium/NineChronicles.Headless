using System.Collections.Generic;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext
    {
        public StateContext(AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter, long blockIndex)
        {
            AccountStateGetter = accountStateGetter;
            AccountBalanceGetter = accountBalanceGetter;
            BlockIndex = blockIndex;
        }

        public AccountStateGetter AccountStateGetter { get; }
        public AccountBalanceGetter AccountBalanceGetter { get; }
        public long BlockIndex { get; }

        public IValue? GetState(Address address) =>
            AccountStateGetter(new[] { address })[0];

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            AccountStateGetter(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            AccountBalanceGetter(address, currency);
    }
}
