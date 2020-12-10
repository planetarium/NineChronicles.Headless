using System;

namespace NineChronicles.Headless.Exceptions
{
    [Serializable]
    public class PeerInvalidException: Exception
    {
        public PeerInvalidException()
        {
        }

        public PeerInvalidException(string message)
            : base(message)
        {
        }

        public PeerInvalidException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
