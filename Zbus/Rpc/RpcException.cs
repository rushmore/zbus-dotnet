using System;
using System.Runtime.Serialization;

namespace Zbus.Rpc
{

    public class RpcException : Exception
    {  
        public RpcException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }

        public RpcException()
        { 
        }

        public RpcException(string message)
            : base(message)
        { 
        }
        public RpcException(string message, Exception inner)
            : base(message, inner)
        { 
        }
    }
}