using Bencodex.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransactionResult
    {
        public readonly TransactionStatus TransactionStatus;
        public readonly long? BlockIndex;
        public readonly string? BlockHash;
        public readonly string? ExceptionName;
        public readonly IValue? ExceptionMetadata;

        public TransactionResult(TransactionStatus status, long? blockIndex, string? blockHash, string? exceptionName, IValue? exceptionMetadata)
        {
            TransactionStatus = status;
            BlockIndex = blockIndex;
            BlockHash = blockHash;
            ExceptionName = exceptionName;
            ExceptionMetadata = exceptionMetadata;
        }
    }
}
