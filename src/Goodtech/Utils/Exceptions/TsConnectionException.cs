using System;
using System.Runtime.Serialization;

namespace Goodtech.Utils.Exceptions
{
    public class TsConnectionException : Exception
    {
        public TsConnectionException()
        {
        }

        public TsConnectionException(string message) : base(message)
        {
        }

        public TsConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TsConnectionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}