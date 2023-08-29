namespace NineChronicles.Headless.Tests.Common
{
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Blocks;

    /// <summary>
    /// A rough replica of https://github.com/planetarium/libplanet/blob/main/Libplanet/State/World.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    [Pure]
    public class MockWorld : IWorld
    {
        private readonly IWorldState _baseState;

        public MockWorld()
            : this(MockWorldState.Empty)
        {
        }

        public MockWorld(IAccount account)
            : this(
                MockWorldState.Empty,
                new MockWorldDelta(
                    ImmutableDictionary<Address, IAccount>.Empty.SetItem(account.Address, account)))
        {
        }

        public MockWorld(IWorldState baseState)
            : this(baseState, new MockWorldDelta())
        {
        }

        private MockWorld(IWorldState baseState, IWorldDelta delta)
        {
            _baseState = baseState;
            Delta = delta;
        }

        /// <inheritdoc/>
        public bool Legacy => true;

        public BlockHash? BlockHash => _baseState.BlockHash;

        /// <inheritdoc/>
        public IWorldDelta Delta { get; private set; }

        public IAccount GetAccount(Address address)
        {
            return Delta.Accounts.TryGetValue(address, out IAccount? account)
                ? account!
                : _baseState.GetAccount(address);
        }

        public IWorld SetAccount(IAccount account)
        {
            if (!account.Address.Equals(ReservedAddresses.LegacyAccount)
                && account.Delta.UpdatedFungibleAssets.Count > 0)
            {
                return this;
            }

            return new MockWorld(
                this,
                new MockWorldDelta(Delta.Accounts.SetItem(account.Address, account)));
        }
    }
}
