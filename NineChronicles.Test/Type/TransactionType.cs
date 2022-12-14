using Libplanet.Explorer.GraphTypes;
using Nekoyume.BlockChain.Policy;

namespace NineChronicles.Test.Type;

public class TransactionResponseType
{
    public TransactionType Transaction { get; set; }
}

public class TransactionType
{
    public long NextTxNonce { get; set; }
    public string SignTransaction { get; set; }
    public TransactionResultType TransactionResult { get; set; }
}

public class TransactionResultType {
    public string TxStatus { get; set; }
    public long BlockIndex { get; set; }
    public string BlockHash { get; set; }
    public string ExceptionName { get; set; }
    public string ExceptionMetadata { get; set; }
    // updatedStates
    // updatedFAV
    // fungibleAssetsDelta
}
