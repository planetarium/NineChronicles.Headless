using System;

namespace Libplanet.Headless
{
    [Serializable]
    public class IceServerInvalidException : Exception
    {
        public IceServerInvalidException()
        {
        }

        public IceServerInvalidException(string message)
            : base(message)
        {
        }

        public IceServerInvalidException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
