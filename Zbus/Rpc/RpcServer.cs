using log4net;
using System;

namespace zbus
{

    public class RpcServer : IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RpcServer));
        public string MqServerAddress { get; set; }
        public string Mq { get; set; }
        public string MqType { get; set; }
        public string Channel { get; set; }
        public int ClientCount { get; set; }  

        public bool AuthEnabled { get; set; } = false;
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }

        private RpcProcessor processor;

        public RpcServer(RpcProcessor processor)
        {
            this.processor = processor;
        }

        public void Start()
        {
            if(Mq == null)
            {
                throw new MissingFieldException("missing mq field");
            }
            if(Channel == null)
            {
                Channel = Mq;
            }

            processor.MountDoc(); 

            MqClient client = new MqClient(MqServerAddress);
            if (AuthEnabled)
            {
                client.AuthEnabled = AuthEnabled;
                client.ApiKey = ApiKey;
                client.SecretKey = SecretKey;
            }

            client.OnOpen += async (cli) =>
            {
                Message msg = new Message();
                msg.Headers[Protocol.CMD] = Protocol.CREATE;
                msg.Headers[Protocol.MQ] = Mq;
                msg.Headers[Protocol.MQ_TYPE] = MqType;
                msg.Headers[Protocol.CHANNEL] = Channel;

                var res = await client.InvokeAsync(msg);
                logger.Info(JsonKit.SerializeObject(res));

                msg = new Message();
                msg.Headers[Protocol.CMD] = Protocol.SUB;
                msg.Headers[Protocol.MQ] = Mq; 
                msg.Headers[Protocol.CHANNEL] = Channel;

                res = await client.InvokeAsync(msg);
                logger.Info(JsonKit.SerializeObject(res));

                msg = new Message();
                msg.Headers[Protocol.CMD] = Protocol.BIND;
                msg.Headers[Protocol.MQ] = Mq;
                msg.Body = processor.UrlEntryList(Mq);

                res = await client.InvokeAsync(msg);
                logger.Info(JsonKit.SerializeObject(res));
            };

            client.AddMqHandler(Mq, Channel, async (request) =>
            {
                string prefix = processor.UrlPrefix;
                string url = request.Url;
                if(url != null && url.StartsWith(prefix))
                {
                    url = url.Substring(prefix.Length);
                    request.Url = PathKit.Join(url);
                }

                string id = (string)request.Headers[Protocol.ID];
                string source = (string)request.Headers[Protocol.SOURCE]; 
                Message response = new Message();
                try
                {
                    await processor.ProcessAsync(request, response); 
                }
                catch(Exception e)
                {
                    while(e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    response.Status = 500;
                    response.Headers["content-type"] = "text/plain; charset=utf8;";
                    response.Body = e.Message;
                }

                response.Headers[Protocol.CMD] = Protocol.ROUTE;
                response.Headers[Protocol.ID] = id;
                response.Headers[Protocol.TARGET] = source; 

                await client.SendAsync(response);
            });

            client.ConnectAsync().Wait(); 
        }
         

        public void Dispose()
        { 
        }
    }
}