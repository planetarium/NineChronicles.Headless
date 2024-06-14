using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Executable.Store;

public class AnonymousStore : IStore
{
#pragma warning disable CS8618
    public Func<IEnumerable<Guid>> ListChainIds { get; set; }
    public Action<Guid> DeleteChainId { get; set; }
    public Func<Guid?> GetCanonicalChainId { get; set; }
    public Action<Guid> SetCanonicalChainId { get; set; }
    public Func<Guid, long> CountIndex { get; set; }
    public Func<Guid, int, int?, IEnumerable<BlockHash>> IterateIndexes { get; set; }
    public Func<Guid, long, BlockHash?> IndexBlockHash { get; set; }
    public Func<Guid, BlockHash, long> AppendIndex { get; set; }
    public Action<Guid, Guid, BlockHash> ForkBlockIndexes { get; set; }
    public Func<TxId, Transaction?> GetTransaction { get; set; }
    public Action<Transaction> PutTransaction { get; set; }
    public Func<IEnumerable<BlockHash>> IterateBlockHashes { get; set; }
    public Func<BlockHash, Block?> GetBlock { get; set; }
    public Func<BlockHash, long?> GetBlockIndex { get; set; }
    public Func<BlockHash, BlockDigest?> GetBlockDigest { get; set; }
    public Action<Block> PutBlock { get; set; }
    public Func<BlockHash, bool> DeleteBlock { get; set; }
    public Func<BlockHash, bool> ContainsBlock { get; set; }
    public Action<TxExecution> PutTxExecution { get; set; }
    public Func<BlockHash, TxId, TxExecution?> GetTxExecution { get; set; }
    public Action<TxId, BlockHash> PutTxIdBlockHashIndex { get; set; }
    public Func<TxId, BlockHash?> GetFirstTxIdBlockHashIndex { get; set; }
    public Func<TxId, IEnumerable<BlockHash>> IterateTxIdBlockHashIndex { get; set; }
    public Action<TxId, BlockHash> DeleteTxIdBlockHashIndex { get; set; }
    public Func<Guid, IEnumerable<KeyValuePair<Address, long>>> ListTxNonces { get; set; }
    public Func<Guid, Address, long> GetTxNonce { get; set; }
    public Action<Guid, Address, long> IncreaseTxNonce { get; set; }
    public Func<TxId, bool> ContainsTransaction { get; set; }
    public Func<long> CountBlocks { get; set; }
    public Action<Guid, Guid> ForkTxNonces { get; set; }
    public Action<bool> PruneOutdatedChains { get; set; }
    public Func<Guid, BlockCommit?> GetChainBlockCommit { get; set; }
    public Action<Guid, BlockCommit> PutChainBlockCommit { get; set; }
    public Func<BlockHash, BlockCommit?> GetBlockCommit { get; set; }
    public Action<BlockCommit> PutBlockCommit { get; set; }
    public Action<BlockHash> DeleteBlockCommit { get; set; }
    public Func<IEnumerable<BlockHash>> GetBlockCommitHashes { get; set; }
    public Func<BlockHash, HashDigest<SHA256>?> GetNextStateRootHash { get; set; }
    public Action<BlockHash, HashDigest<SHA256>> PutNextStateRootHash { get; set; }
    public Action<BlockHash> DeleteNextStateRootHash { get; set; }
#pragma warning restore CS8618

    void IDisposable.Dispose()
    {
    }

    IEnumerable<Guid> IStore.ListChainIds()
    {
        return ListChainIds();
    }

    void IStore.DeleteChainId(Guid chainId)
    {
        DeleteChainId(chainId);
    }

    Guid? IStore.GetCanonicalChainId()
    {
        return GetCanonicalChainId();
    }

    void IStore.SetCanonicalChainId(Guid chainId)
    {
        SetCanonicalChainId(chainId);
    }

    long IStore.CountIndex(Guid chainId)
    {
        return CountIndex(chainId);
    }

    IEnumerable<BlockHash> IStore.IterateIndexes(Guid chainId, int offset, int? limit)
    {
        return IterateIndexes(chainId, offset, limit);
    }

    BlockHash? IStore.IndexBlockHash(Guid chainId, long index)
    {
        return IndexBlockHash(chainId, index);
    }

    long IStore.AppendIndex(Guid chainId, BlockHash hash)
    {
        return AppendIndex(chainId, hash);
    }

    void IStore.ForkBlockIndexes(Guid sourceChainId, Guid destinationChainId, BlockHash branchpoint)
    {
        ForkBlockIndexes(sourceChainId, destinationChainId, branchpoint);
    }

    Transaction? IStore.GetTransaction(TxId txid)
    {
        return GetTransaction(txid);
    }

    void IStore.PutTransaction(Transaction tx)
    {
        PutTransaction(tx);
    }

    IEnumerable<BlockHash> IStore.IterateBlockHashes()
    {
        return IterateBlockHashes();
    }

    Block? IStore.GetBlock(BlockHash blockHash)
    {
        return GetBlock(blockHash);
    }

    long? IStore.GetBlockIndex(BlockHash blockHash)
    {
        return GetBlockIndex(blockHash);
    }

    BlockDigest? IStore.GetBlockDigest(BlockHash blockHash)
    {
        return GetBlockDigest(blockHash);
    }

    void IStore.PutBlock(Block block)
    {
        PutBlock(block);
    }

    bool IStore.DeleteBlock(BlockHash blockHash)
    {
        return DeleteBlock(blockHash);
    }

    bool IStore.ContainsBlock(BlockHash blockHash)
    {
        return ContainsBlock(blockHash);
    }

    void IStore.PutTxExecution(TxExecution txExecution)
    {
        PutTxExecution(txExecution);
    }

    TxExecution? IStore.GetTxExecution(BlockHash blockHash, TxId txid)
    {
        return GetTxExecution(blockHash, txid);
    }

    void IStore.PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        PutTxIdBlockHashIndex(txId, blockHash);
    }

    BlockHash? IStore.GetFirstTxIdBlockHashIndex(TxId txId)
    {
        return GetFirstTxIdBlockHashIndex(txId);
    }

    IEnumerable<BlockHash> IStore.IterateTxIdBlockHashIndex(TxId txId)
    {
        return IterateTxIdBlockHashIndex(txId);
    }

    void IStore.DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash)
    {
        DeleteTxIdBlockHashIndex(txId, blockHash);
    }

    IEnumerable<KeyValuePair<Address, long>> IStore.ListTxNonces(Guid chainId)
    {
        return ListTxNonces(chainId);
    }

    long IStore.GetTxNonce(Guid chainId, Address address)
    {
        return GetTxNonce(chainId, address);
    }

    void IStore.IncreaseTxNonce(Guid chainId, Address signer, long delta)
    {
        IncreaseTxNonce(chainId, signer, delta);
    }

    bool IStore.ContainsTransaction(TxId txId)
    {
        return ContainsTransaction(txId);
    }

    long IStore.CountBlocks()
    {
        return CountBlocks();
    }

    void IStore.ForkTxNonces(Guid sourceChainId, Guid destinationChainId)
    {
        ForkTxNonces(sourceChainId, destinationChainId);
    }

    void IStore.PruneOutdatedChains(bool noopWithoutCanon)
    {
        PruneOutdatedChains(noopWithoutCanon);
    }

    BlockCommit? IStore.GetChainBlockCommit(Guid chainId)
    {
        return GetChainBlockCommit(chainId);
    }

    void IStore.PutChainBlockCommit(Guid chainId, BlockCommit blockCommit)
    {
        PutChainBlockCommit(chainId, blockCommit);
    }

    BlockCommit? IStore.GetBlockCommit(BlockHash blockHash)
    {
        return GetBlockCommit(blockHash);
    }

    void IStore.PutBlockCommit(BlockCommit blockCommit)
    {
        PutBlockCommit(blockCommit);
    }

    void IStore.DeleteBlockCommit(BlockHash blockHash)
    {
        DeleteBlockCommit(blockHash);
    }

    IEnumerable<BlockHash> IStore.GetBlockCommitHashes()
    {
        return GetBlockCommitHashes();
    }

    HashDigest<SHA256>? IStore.GetNextStateRootHash(BlockHash blockHash)
    {
        return GetNextStateRootHash(blockHash);
    }

    void IStore.PutNextStateRootHash(BlockHash blockHash, HashDigest<SHA256> nextStateRootHash)
    {
        PutNextStateRootHash(blockHash, nextStateRootHash);
    }

    void IStore.DeleteNextStateRootHash(BlockHash blockHash)
    {
        DeleteNextStateRootHash(blockHash);
    }
}
