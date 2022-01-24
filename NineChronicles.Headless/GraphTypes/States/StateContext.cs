using System.Collections.Generic;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext
    {
        public StateContext(AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter)
        {
            AccountStateGetter = accountStateGetter;
            AccountBalanceGetter = accountBalanceGetter;
        }

        public AccountStateGetter AccountStateGetter { get; }
        public AccountBalanceGetter AccountBalanceGetter { get; }

        public IValue? GetState(Address address) =>
            AccountStateGetter(new[] { address })[0];

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            AccountStateGetter(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            AccountBalanceGetter(address, currency);
    }
}
