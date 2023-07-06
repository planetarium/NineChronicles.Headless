using Bencodex.Types;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.Consensus;

namespace Libplanet.Extensions.RemoteBlockChainStates;

public class RemoteBlockStates : IBlockStates
{
    private readonly RemoteBlockChainStates _blockChainStates;
    public BlockHash? BlockHash { get; private set; }

    public RemoteBlockStates(RemoteBlockChainStates blockChainStates, BlockHash blockHash)
    {
        _blockChainStates = blockChainStates;
        this.BlockHash = blockHash;
    }

    public IValue? GetState(Address address)
    {
        return _blockChainStates.GetState(address, BlockHash);
    }

    public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
    {
        return _blockChainStates.GetStates(addresses, BlockHash);
    }

    public FungibleAssetValue GetBalance(Address address, Currency currency)
    {
        return _blockChainStates.GetBalance(address, currency, BlockHash);
    }

    public FungibleAssetValue GetTotalSupply(Currency currency)
    {
        return _blockChainStates.GetTotalSupply(currency, BlockHash);
    }

    public ValidatorSet GetValidatorSet()
    {
        return _blockChainStates.GetValidatorSet(BlockHash);
    }
}
