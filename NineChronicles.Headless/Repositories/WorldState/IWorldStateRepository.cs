using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Types.Blocks;

namespace NineChronicles.Headless.Repositories.WorldState;

public interface IWorldStateRepository
{
    IWorldState GetWorldState(long index);
    IWorldState GetWorldState(BlockHash blockHash);
    IWorldState GetWorldState(HashDigest<SHA256> stateRootHash);
}
