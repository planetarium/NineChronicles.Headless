namespace NineChronicles.Headless.Tests.Common
{
    using System.Collections.Immutable;
    using Libplanet.Action.State;
    using Libplanet.Crypto;

    /// <summary>
    /// Almost a replica of https://github.com/planetarium/libplanet/blob/main/Libplanet/State/WorldDelta.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    public class MockWorldDelta : IWorldDelta
    {
        public MockWorldDelta()
        {
            Accounts = ImmutableDictionary<Address, IAccount>.Empty;
        }

        public MockWorldDelta(IImmutableDictionary<Address, IAccount> accounts)
        {
            Accounts = accounts;
        }

        /// <inheritdoc cref="IWorldDelta.UpdatedAddresses"/>
        public IImmutableSet<Address> UpdatedAddresses =>
            Accounts.Keys.ToImmutableHashSet();

        /// <inheritdoc cref="IWorldDelta.Accounts"/>
        public IImmutableDictionary<Address, IAccount> Accounts { get; }
    }
}
