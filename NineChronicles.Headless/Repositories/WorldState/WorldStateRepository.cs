namespace NineChronicles.Headless.Repositories.WorldState;

using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Types.Blocks;
using Libplanet.Blockchain;

public class WorldStateRepository : IWorldStateRepository
{
    private readonly BlockChain _blockChain;

    public WorldStateRepository(BlockChain blockChain)
    {
        _blockChain = blockChain;
    }

    public IWorldState GetWorldState(long index)
    {
        return _blockChain.GetWorldState(_blockChain[index].StateRootHash);
    }

    public IWorldState GetWorldState(BlockHash blockHash)
    {
        return _blockChain.GetWorldState(blockHash);
    }

    public IWorldState GetWorldState(HashDigest<SHA256> stateRootHash)
    {
        return _blockChain.GetWorldState(stateRootHash);
    }
}
