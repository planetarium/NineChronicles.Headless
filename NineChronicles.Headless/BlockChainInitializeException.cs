using System;

namespace NineChronicles.Headless
{
    public class BlockChainInitializeException : Exception
    {
        public BlockChainInitializeException(string message)
            : base(message)
        {
        }
    }
}
