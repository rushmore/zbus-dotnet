using System;

namespace Zbus.Rpc
{ 
    public class Response
    {
        public dynamic Result { get; set; }
        /// <summary>
        /// With value indicates Error returned, otherwise No error, check Result, it is a json value(empty included)
        /// </summary> 
        public object Error { get; set; }
    }
}