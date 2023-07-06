namespace NineChronicles.Headless.Executable.Commands;

public partial class ReplayCommand
{
#pragma warning disable S3459
    private sealed class GetTransactionResponse
    {
        public ChainQueryType? ChainQuery { get; set; }
        public TransactionType? Transaction { get; set; }
    }
    
    private sealed class ChainQueryType
    {
        public TransactionQueryType? TransactionQuery { get; set; }
    }

    private sealed class TransactionQueryType
    {
        public TransactionQueryTransactionType? Transaction { get; set; }
    }
    
    private sealed class TransactionQueryTransactionType
    {
        public string? SerializedPayload { get; set; }
    }

    private sealed class TransactionType
    {
        public TransactionTransactionResultType? TransactionResult { get; set; }
    }

    private sealed class TransactionTransactionResultType
    {
        public string? TxStatus { get; set; }
        public int? BlockIndex { get; set; }
        public string? BlockHash { get; set; }
    }
#pragma warning restore S3459
}
