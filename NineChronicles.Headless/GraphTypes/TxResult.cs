using Bencodex.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class TxResult
    {
        public readonly TxStatus TxStatus;
        public readonly long? BlockIndex;
        public readonly string? BlockHash;
        public readonly string? ExceptionName;
        public readonly IValue? ExceptionMetadata;

        public TxResult(TxStatus status, long? blockIndex, string? blockHash, string? exceptionName, IValue? exceptionMetadata)
        {
            TxStatus = status;
            BlockIndex = blockIndex;
            BlockHash = blockHash;
            ExceptionName = exceptionName;
            ExceptionMetadata = exceptionMetadata;
        }
    }
}
