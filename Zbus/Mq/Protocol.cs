using System;
using System.Collections.Generic;

namespace zbus
{
    public class Protocol
    {
        public static readonly string CMD = "cmd";
        public static readonly string PUB = "pub";
        public static readonly string SUB = "sub";
        public static readonly string ROUTE = "route";
        public static readonly string CREATE = "create";
        public static readonly string BIND = "bind";
        public static readonly string PING = "ping";

        public static readonly string MQ = "mq";
        public static readonly string CHANNEL = "channel";
        public static readonly string MQ_TYPE = "mqType";

        public static readonly string STATUS = "status";
        public static readonly string BODY = "body";
        public static readonly string ID = "id";
        public static readonly string ACK = "ack";
        public static readonly string SOURCE = "source";
        public static readonly string TARGET = "target";
        public static readonly string WINDOW = "window";

    } 
     
}
