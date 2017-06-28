using System;
using System.Runtime.Serialization;

namespace Zbus.Mq
{
    public class MqException : Exception
    {   
        public MqException(): base()
        { 

        } 
        public MqException(string message): base(message)
        { 

        }
        public MqException(string message, Exception inner): base(message, inner)
        { 

        }
        public MqException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }
    }
}