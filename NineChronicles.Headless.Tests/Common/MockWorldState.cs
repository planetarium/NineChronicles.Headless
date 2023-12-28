using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Tests.Common
{
#nullable enable

    using System.Collections.Immutable;
    using Libplanet.Action.State;
    using Libplanet.Crypto;

    public class MockWorldState : IWorldState
    {
        private readonly IImmutableDictionary<Address, IAccount> _accounts;

        public MockWorldState()
            : this(ImmutableDictionary<Address, IAccount>.Empty)
        {
        }

        public MockWorldState(IImmutableDictionary<Address, IAccount> accounts)
        {
            _accounts = accounts;
        }

        public ITrie Trie { get; }
        public bool Legacy => true;

        public IImmutableDictionary<Address, IAccount> Accounts => _accounts;

        public IAccount GetAccount(Address address) => _accounts.TryGetValue(address, out IAccount? account)
            ? account
            : new MockAccount(new MockAccountState());
    }
}
