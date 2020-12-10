using System;

namespace NineChronicles.Headless.Exceptions
{
    [Serializable]
    public class IceServerInvalidException: Exception
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
