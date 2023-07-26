using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Headless
{
    /// <summary>
    /// A <see cref="IStore"/> decorator that reduce space consumption by omitting input calls which
    /// are unused by Nine Chronicles.
    /// <para>Calls on this will be forwarded to its <see cref="InternalStore"/>, except for:</para>
    /// <list type="bullet">
    /// <item><description><see cref="PutTxExecution(TxSuccess)"/></description></item>
    /// </list>
    /// </summary>
    public sealed class ReducedStore : IStore
    {
        public ReducedStore(IStore internalStore)
        {
            InternalStore = internalStore;
        }

        public IStore InternalStore { get; }

        public long AppendIndex(Guid chainId, BlockHash hash) =>
            InternalStore.AppendIndex(chainId, hash);

        public bool ContainsBlock(BlockHash blockHash) =>
            InternalStore.ContainsBlock(blockHash);

        public bool ContainsTransaction(TxId txId) =>
            InternalStore.ContainsTransaction(txId);

        public long CountBlocks() =>
            InternalStore.CountBlocks();

        public long CountIndex(Guid chainId) =>
            InternalStore.CountIndex(chainId);

        public bool DeleteBlock(BlockHash blockHash) =>
            InternalStore.DeleteBlock(blockHash);

        public void DeleteChainId(Guid chainId) =>
            InternalStore.DeleteChainId(chainId);

        public void ForkBlockIndexes(
            Guid sourceChainId,
            Guid destinationChainId,
            BlockHash branchpoint
        ) =>
            InternalStore.ForkBlockIndexes(sourceChainId, destinationChainId, branchpoint);

        public void ForkTxNonces(Guid sourceChainId, Guid destinationChainId) =>
            InternalStore.ForkTxNonces(sourceChainId, destinationChainId);

        public Block GetBlock(BlockHash blockHash)
            => InternalStore.GetBlock(blockHash);

        public BlockDigest? GetBlockDigest(BlockHash blockHash) =>
            InternalStore.GetBlockDigest(blockHash);

        public long? GetBlockIndex(BlockHash blockHash) =>
            InternalStore.GetBlockIndex(blockHash);

        public Guid? GetCanonicalChainId() =>
            InternalStore.GetCanonicalChainId();

        public Transaction GetTransaction(TxId txid) =>
            InternalStore.GetTransaction(txid);

        public TxExecution GetTxExecution(BlockHash blockHash, TxId txid) =>
            InternalStore.GetTxExecution(blockHash, txid);

        public long GetTxNonce(Guid chainId, Address address) =>
            InternalStore.GetTxNonce(chainId, address);

        public void IncreaseTxNonce(Guid chainId, Address signer, long delta = 1) =>
            InternalStore.IncreaseTxNonce(chainId, signer, delta);

        public BlockHash? IndexBlockHash(Guid chainId, long index) =>
            InternalStore.IndexBlockHash(chainId, index);

        public IEnumerable<BlockHash> IterateBlockHashes() =>
            InternalStore.IterateBlockHashes();

        public IEnumerable<BlockHash> IterateIndexes(
            Guid chainId,
            int offset = 0,
            int? limit = null
        ) =>
            InternalStore.IterateIndexes(chainId, offset, limit);

        public IEnumerable<Guid> ListChainIds() =>
            InternalStore.ListChainIds();

        public IEnumerable<KeyValuePair<Address, long>> ListTxNonces(Guid chainId) =>
            InternalStore.ListTxNonces(chainId);

        public void PutBlock(Block block) =>
            InternalStore.PutBlock(block);

        public void PutTransaction(Transaction tx) =>
            InternalStore.PutTransaction(tx);

        public void PutTxExecution(TxSuccess txSuccess)
        {
            // Omit TxSuccess.UpdatedStates as it is unused by Nine Chronicles and too big.
            TxSuccess reducedTxSuccess = new TxSuccess(
                txSuccess.BlockHash,
                txSuccess.TxId,
                updatedStates: txSuccess.UpdatedStates.ToImmutableDictionary(pair => pair.Key, _ => (IValue)Null.Value),
                fungibleAssetsDelta: txSuccess.FungibleAssetsDelta,
                updatedFungibleAssets: txSuccess.UpdatedFungibleAssets
            );
            InternalStore.PutTxExecution(reducedTxSuccess);
        }

        public void PutTxExecution(TxFailure txFailure) =>
            InternalStore.PutTxExecution(txFailure);

        public void SetCanonicalChainId(Guid chainId) =>
            InternalStore.SetCanonicalChainId(chainId);

        public void PutTxIdBlockHashIndex(TxId txId, BlockHash blockHash) =>
            InternalStore.PutTxIdBlockHashIndex(txId, blockHash);

        public BlockHash? GetFirstTxIdBlockHashIndex(TxId txId) =>
            InternalStore.GetFirstTxIdBlockHashIndex(txId);

        public IEnumerable<BlockHash> IterateTxIdBlockHashIndex(TxId txId) =>
            InternalStore.IterateTxIdBlockHashIndex(txId);

        public void DeleteTxIdBlockHashIndex(TxId txId, BlockHash blockHash) =>
            InternalStore.DeleteTxIdBlockHashIndex(txId, blockHash);

        public void PruneOutdatedChains(bool noopWithoutCanon = false) =>
            InternalStore.PruneOutdatedChains(noopWithoutCanon);

        public BlockCommit GetChainBlockCommit(Guid chainId) =>
            InternalStore.GetChainBlockCommit(chainId);

        public void PutChainBlockCommit(Guid chainId, BlockCommit blockCommit) =>
            InternalStore.PutChainBlockCommit(chainId, blockCommit);

        public BlockCommit GetBlockCommit(BlockHash blockHash) =>
            InternalStore.GetBlockCommit(blockHash);

        public void PutBlockCommit(BlockCommit blockCommit) =>
            InternalStore.PutBlockCommit(blockCommit);

        public void DeleteBlockCommit(BlockHash blockHash) =>
            InternalStore.DeleteBlockCommit(blockHash);

        public IEnumerable<BlockHash> GetBlockCommitHashes() =>
            InternalStore.GetBlockCommitHashes();

        public void Dispose() => InternalStore.Dispose();
    }
}
