namespace NineChronicles.Headless.Repositories.Transaction;

using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

public interface ITransactionRepository
{
    Transaction? GetTransaction(TxId txId);
    TxExecution? GetTxExecution(BlockHash blockHash, TxId txId);
    long GetNextTxNonce(Address address);
}
