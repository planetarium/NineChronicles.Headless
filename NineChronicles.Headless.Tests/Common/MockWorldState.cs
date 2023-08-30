namespace NineChronicles.Headless.Tests.Common
{
#nullable enable

    using System.Collections.Immutable;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Blocks;

    public class MockWorldState : IWorldState
    {
        private static readonly MockWorldState _empty = new MockWorldState();
        private readonly IImmutableDictionary<Address, IAccount> _accounts;

        private MockWorldState()
            : this(ImmutableDictionary<Address, IAccount>.Empty)
        {
        }

        private MockWorldState(IImmutableDictionary<Address, IAccount> accounts)
        {
            _accounts = accounts;
        }

        public static MockWorldState Empty => _empty;

        public bool Legacy => true;

        public BlockHash? BlockHash => null;

        public IImmutableDictionary<Address, IAccount> Accounts => _accounts;

        public IAccount GetAccount(Address address) => _accounts.TryGetValue(address, out IAccount? account)
            ? account
            : new MockAccount(address);
    }
}
