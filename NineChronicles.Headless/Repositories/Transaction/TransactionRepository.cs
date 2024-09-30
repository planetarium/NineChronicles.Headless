namespace NineChronicles.Headless.Repositories.Transaction;

using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

public class TransactionRepository : ITransactionRepository
{
    private readonly BlockChain _blockChain;

    public TransactionRepository(BlockChain blockChain)
    {
        _blockChain = blockChain;
    }

    public Transaction? GetTransaction(TxId txId)
    {
        return _blockChain.GetTransaction(txId);
    }

    public TxExecution? GetTxExecution(BlockHash blockHash, TxId txId)
    {
        return _blockChain.GetTxExecution(blockHash, txId);
    }

    public long GetNextTxNonce(Address address)
    {
        return _blockChain.GetNextTxNonce(address);
    }
}
