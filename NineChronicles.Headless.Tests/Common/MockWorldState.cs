using System.Collections.Immutable;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;

namespace NineChronicles.Headless.Tests.Common;

public class MockWorldState : IWorldState
{
    private static readonly MockWorldState _empty = new MockWorldState();
    private readonly IImmutableDictionary<Address, IAccount> _accounts;
    
    public MockWorldState()
        : this(ImmutableDictionary<Address, IAccount>.Empty)
    {
        Legacy = true;
    }

    public MockWorldState(IImmutableDictionary<Address, IAccount> accounts)
    {
        _accounts = accounts;
    }

    public static MockWorldState Empty => _empty;

    public IImmutableDictionary<Address, IAccount> Accounts => _accounts;
    
    public BlockHash? BlockHash { get; }
    
    public bool Legacy { get; }

    public IAccount GetAccount(Address address) =>
        _accounts.TryGetValue(address, out IAccount? value)
        ? value
        : null;
}
