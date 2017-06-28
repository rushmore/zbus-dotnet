using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zbus.Mq
{
    public class Producer : MqAdmin
    {
        public ServerSelector ProduceSelector { get; set; }

        public Producer(Broker broker) : base(broker)
        {
            ProduceSelector = (routeTable, msg) =>
            {
                if (msg.Topic == null)
                {
                    throw new MqException("Missing Topic");
                }
                var topicTable = routeTable.TopicTable;
                if (routeTable.ServerTable.Count < 1)
                {
                    return new ServerAddress[0];
                }
                if(!topicTable.ContainsKey(msg.Topic))
                {
                    return new ServerAddress[0];
                }
                IList<TopicInfo> topicServerList = topicTable[msg.Topic];
                if(topicServerList.Count < 1)
                {
                    return new ServerAddress[0];
                }

                TopicInfo target = topicServerList[0];
                foreach(TopicInfo current in topicServerList)
                {
                    if(target.ConsumerCount < current.ConsumerCount)
                    {
                        target = current;
                    }
                }
                return new ServerAddress[] { target.ServerAddress };
            };
        } 

        public async Task<Message> ProduceAsync(Message msg, CancellationToken? token = null, ServerSelector selector = null)
        {
            msg.Cmd = Protocol.PRODUCE; 
            if(msg.Token == null)
            {
                msg.Token = Token;
            }
            if(selector == null)
            {
                selector = ProduceSelector;
            }

            MqClientPool[] pools = broker.Select(selector, msg);
            if(pools.Length < 1)
            {
                throw new MqException("Missing MqServer for topic: " + msg.Topic);
            }
            var pool = pools[0];
            MqClient client = null;
            try
            {
                client = pool.Borrow();
                return await client.InvokeAsync(msg, token);
            } 
            finally
            {
                if (client != null)
                {
                    pool.Return(client);
                }
            }
        } 
    }
}
