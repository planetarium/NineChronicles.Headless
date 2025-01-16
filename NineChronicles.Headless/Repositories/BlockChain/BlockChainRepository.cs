using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Repositories.BlockChain;

using System.Collections.Generic;
using Libplanet.Blockchain;
using Libplanet.Types.Blocks;
using Block = NineChronicles.Headless.Domain.Model.BlockChain.Block;
using LibplanetBlock = Libplanet.Types.Blocks.Block;

public class BlockChainRepository : IBlockChainRepository
{
    private readonly BlockChain _blockChain;

    private static Block Convert(LibplanetBlock block)
    {
        return new Block(
            block.Hash,
            block.PreviousHash,
            block.Miner,
            block.Index,
            block.Timestamp,
            block.StateRootHash,
            block.Transactions
        );
    }

    public BlockChainRepository(BlockChain blockChain)
    {
        _blockChain = blockChain;
    }

    public Block GetTip()
    {
        return FetchTip();
    }

    public Block GetBlock(long index)
    {
        return FetchBlock(index);
    }

    public Block GetBlock(BlockHash blockHash)
    {
        return FetchBlock(blockHash);
    }

    public IEnumerable<Block> IterateBlocksDescending(long offset)
    {
        Block block = FetchTip();

        while (offset > 0)
        {
            offset--;
            if (block.PreviousHash is { } prev)
            {
                block = FetchBlock(prev);
            }
        }

        while (true)
        {
            yield return block;
            if (block.PreviousHash is { } prev)
            {
                block = FetchBlock(prev);
            }
            else
            {
                break;
            }
        }
    }

    public bool StageTransaction(Libplanet.Types.Tx.Transaction tx)
    {
        return _blockChain.StageTransaction(tx);
    }

    public Exception? ValidateNextBlockTx(Libplanet.Types.Tx.Transaction tx)
    {
        return _blockChain.Policy.ValidateNextBlockTx(_blockChain, tx);
    }

    public IImmutableSet<TxId> GetStagedTransactionIds()
    {
        return _blockChain.GetStagedTransactionIds();
    }

    public IWorldState GetWorldState(HashDigest<SHA256> stateRootHash)
    {
        return _blockChain.GetWorldState(stateRootHash);
    }

    public IWorldState GetWorldState(BlockHash blockHash)
    {
        return _blockChain.GetWorldState(blockHash);
    }

    public long GetNextTxNonce(Address address)
    {
        return _blockChain.GetNextTxNonce(address);
    }

    public LibplanetBlock Tip => _blockChain.Tip;

    private Block FetchTip()
    {
        return Convert(_blockChain.Tip);
    }

    private Block FetchBlock(BlockHash blockHash)
    {
        return Convert(_blockChain[blockHash]);
    }

    private Block FetchBlock(long index)
    {
        return Convert(_blockChain[index]);
    }
}
