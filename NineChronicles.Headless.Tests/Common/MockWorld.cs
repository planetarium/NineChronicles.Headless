using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Tests.Common
{
    using System.Diagnostics.Contracts;
    using Libplanet.Action.State;
    using Libplanet.Crypto;

    /// <summary>
    /// A rough replica of https://github.com/planetarium/libplanet/blob/main/Libplanet.Action/State/World.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    [Pure]
    public class MockWorld : IWorld
    {
        private readonly IWorldState _baseState;

        public MockWorld(IWorldState baseState)
            : this(baseState, new MockWorldDelta())
        {
        }

        public MockWorld(
            IWorldState baseState,
            IWorldDelta delta)
        {
            _baseState = baseState;
            Delta = delta;
            Legacy = baseState.Legacy;
        }

        /// <inheritdoc/>
        public IWorldDelta Delta { get; }

        /// <inheritdoc/>
        [Pure]
        public ITrie Trie => _baseState.Trie;

        /// <inheritdoc/>
        [Pure]
        public bool Legacy { get; private set; }

        /// <inheritdoc/>
        [Pure]
        public IAccount GetAccount(Address address)
        {
            return Delta.Accounts.TryGetValue(address, out IAccount? account)
                ? account
                : _baseState.GetAccount(address);
        }

        /// <inheritdoc/>
        [Pure]
        public IWorld SetAccount(Address address, IAccount account)
        {
            if (!address.Equals(ReservedAddresses.LegacyAccount)
                && account.TotalUpdatedFungibleAssets.Count > 0)
            {
                return this;
            }

            return new World(
                this,
                Delta.SetAccount(address, account));
        }
    }
}
