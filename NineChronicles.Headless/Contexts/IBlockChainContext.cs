using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;

namespace NineChronicles.Headless.Contexts;

public interface IBlockChainContext
{
    Block GetBlock(long blockIndex);
    Block GetBlock(BlockHash blockHash);
    Block GetTip();
    long GetNextTxNonce(Address address);
}
