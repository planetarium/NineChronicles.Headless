using System.Collections.Generic;
using Libplanet.Types.Blocks;

namespace NineChronicles.Headless.Repositories.BlockChain;

using Block = NineChronicles.Headless.Domain.Model.BlockChain.Block;

public interface IBlockChainRepository
{
    Block GetTip();
    Block GetBlock(long index);
    Block GetBlock(BlockHash blockHash);
    IEnumerable<Block> IterateBlocksDescending(long offset);
}
