using log4net;
using System;
using System.Collections.Generic;

namespace zbus
{
    public class MqClient : WebsocketClient
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(MqClient));

        private IList<MqHandler> handlers = new List<MqHandler>();
        public MqClient(string address) : base(address)
        {
            this.heartbeatMessage = new Message();
            this.heartbeatMessage.Headers[Protocol.CMD] = Protocol.PING; 

            OnMessage += async (msg) =>{
                string mq = (string)msg.Headers[Protocol.MQ];
                string channel = (string)msg.Headers[Protocol.CHANNEL];
                if(mq == null || channel == null)
                {
                    logger.Warn("Missing mq or channel in response: " + JsonKit.SerializeObject(msg));
                    return;
                }
                MqHandler mqHandler = null;
                foreach(var e in this.handlers)
                {
                    if(e.Mq == mq && e.Channel == channel)
                    {
                        mqHandler = e;
                        break;
                    }
                }
                if(mqHandler == null)
                {
                    logger.Warn(string.Format("Missing handler for mq={}, channel={}", mq, channel));
                    return;
                }
                mqHandler.Handler(msg);

                string windowStr = (string)msg.Headers[Protocol.WINDOW];
                int? window = null;
                if (windowStr != null) window = int.Parse(windowStr);
                if(window != null && window <= mqHandler.Window / 2)
                {
                    var sub = new Message();
                    sub.Headers[Protocol.CMD] = Protocol.SUB;
                    sub.Headers[Protocol.MQ] = mq;
                    sub.Headers[Protocol.CHANNEL] = channel;
                    sub.Headers[Protocol.ACK] = false;
                    sub.Headers[Protocol.WINDOW] = mqHandler.Window.ToString();

                    await this.SendAsync(sub);
                }  
            };
        } 

        public void AddMqHandler(string mq, string channel, Action<Message> handler, int window=1)
        {
            foreach (var e in this.handlers)
            {
                if (e.Mq == mq && e.Channel == channel)
                {
                    e.Handler = handler;
                    e.Window = window;
                    return;
                }
            }

            var mqHandler = new MqHandler
            {
                Mq = mq,
                Channel = channel,
                Window = window,
                Handler = handler
            };
            this.handlers.Add(mqHandler);
        }

        public class MqHandler
        {
            public string Mq { get; set; }
            public string Channel { get; set; }
            public int Window { get; set; } = 1;
            public Action<Message> Handler { get; set; }
        }
    }


}
