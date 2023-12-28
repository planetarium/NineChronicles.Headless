using System.Linq;

namespace NineChronicles.Headless.Tests.Common
{
    using System.Collections.Immutable;
    using Libplanet.Action.State;
    using Libplanet.Crypto;

    /// <summary>
    /// Almost a replica of https://github.com/planetarium/libplanet/blob/main/Libplanet.Action/State/WorldDelta.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    public class MockWorldDelta : IWorldDelta
    {
        private IImmutableDictionary<Address, AccountItem> _accounts;

        public MockWorldDelta()
        {
            _accounts = ImmutableDictionary<Address, AccountItem>.Empty;
        }

        private MockWorldDelta(IImmutableDictionary<Address, AccountItem> accounts)
        {
            _accounts = accounts;
        }

        /// <inheritdoc cref="IWorldDelta.Accounts"/>
        public IImmutableDictionary<Address, IAccount> Accounts
            => _accounts
                .ToImmutableDictionary(item => item.Key, item => item.Value.Account);

        /// <inheritdoc cref="IWorldDelta.Uncommitted"/>
        public IImmutableDictionary<Address, IAccount> Uncommitted
            => _accounts
                .Where(item => !item.Value.Committed)
                .ToImmutableDictionary(item => item.Key, item => item.Value.Account);

        /// <inheritdoc cref="IWorldDelta.SetAccount"/>
        public IWorldDelta SetAccount(Address address, IAccount account)
            => new MockWorldDelta(_accounts.SetItem(address, new AccountItem(account, false)));

        /// <inheritdoc cref="IWorldDelta.CommitAccount"/>
        public IWorldDelta CommitAccount(Address address)
            => _accounts.TryGetValue(address, out AccountItem accountItem)
                ? new MockWorldDelta(
                    _accounts.SetItem(address, new AccountItem(accountItem.Account, true)))
                : this;

        internal struct AccountItem
        {
            public AccountItem(IAccount account, bool committed)
            {
                Account = account;
                Committed = committed;
            }

            public IAccount Account { get; }

            public bool Committed { get; set; }
        }
    }
}
