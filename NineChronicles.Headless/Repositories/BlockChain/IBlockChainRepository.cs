using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Repositories.BlockChain;

using Block = NineChronicles.Headless.Domain.Model.BlockChain.Block;

public interface IBlockChainRepository
{
    Block GetTip();
    Block GetBlock(long index);
    Block GetBlock(BlockHash blockHash);
    IEnumerable<Block> IterateBlocksDescending(long offset);
    bool StageTransaction(Libplanet.Types.Tx.Transaction tx);
    Exception? ValidateNextBlockTx(Libplanet.Types.Tx.Transaction tx);
    IImmutableSet<TxId> GetStagedTransactionIds();
    Libplanet.Types.Blocks.Block Tip { get; }
}
