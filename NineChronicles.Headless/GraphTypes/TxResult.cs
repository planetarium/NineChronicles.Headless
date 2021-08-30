namespace NineChronicles.Headless.GraphTypes
{
    public class TxResult
    {
        public readonly TxStatus TxStatus;
        public readonly long? BlockIndex;
        public readonly string? BlockHash;

        public TxResult(TxStatus status, long? blockIndex, string? blockHash)
        {
            TxStatus = status;
            BlockIndex = blockIndex;
            BlockHash = blockHash;
        }
    }
}
