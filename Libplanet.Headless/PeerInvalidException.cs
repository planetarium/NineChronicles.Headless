using System;

namespace Libplanet.Headless
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
