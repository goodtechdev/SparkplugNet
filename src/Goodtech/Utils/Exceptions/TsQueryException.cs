using System;
using System.Runtime.Serialization;

namespace Goodtech.Utils.Exceptions
{
    public class TsQueryException : Exception
    {
        public TsQueryException()
        {
        }

        public TsQueryException(string message) : base(message)
        {
        }

        public TsQueryException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TsQueryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}