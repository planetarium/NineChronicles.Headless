namespace Libplanet.Standalone
{
    public enum NodeExceptionType
    {
        NoAnyPeer = 0x01,
        
        DemandTooHigh = 0x02,
        
        TipNotChange = 0x03,
        
        MessageNotReceived = 0x04,
        
        ActionTimeout = 0x05,
    }
    
    public class NodeException
    {
        public readonly int Code;
        public readonly string Message;
        
        public NodeException(NodeExceptionType code, string message)
        {
            Code = (int)code;
            Message = message;
        }
    }
}
