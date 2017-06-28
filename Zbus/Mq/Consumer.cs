using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zbus.Mq
{
    public class Consumer : MqAdmin
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Consumer));

        public ServerSelector ConsumeSelector { get; set; }
        public Action<Message, MqClient> MessageHandler;
        public int? ConnectionCount { get; set; }
        public string Topic { get; private set; }
        public ConsumeGroup ConsumeGroup { get; set; }

        private IDictionary<ServerAddress, ConsumeThread> consumeThreadTable;

        public Consumer(Broker broker, string topic) : base(broker)
        {
            ConsumeSelector = (routeTable, msg) =>
            {
                return routeTable.ServerTable.Keys.ToArray<ServerAddress>();
            };
            Topic = topic;
        }

        private void AddConsumeThread(MqClientPool pool)
        {  
            ServerAddress serverAddress = pool.ServerAddress;
            if (consumeThreadTable.ContainsKey(serverAddress)) return;

            //TODO configure more on thread
            ConsumeThread thread = new ConsumeThread(Topic, ConsumeGroup)
            {
                ClientFactory = () =>
                {
                    return pool.Create();
                },  
            };
            thread.MessageRecevied += (msg, client) =>  MessageHandler?.Invoke(msg, client); 

            consumeThreadTable[serverAddress] = thread;
            thread.Start(); 
        }

        public void Start()
        {
            lock (this)
            {
                if (consumeThreadTable != null) return;
                consumeThreadTable = new ConcurrentDictionary<ServerAddress, ConsumeThread>(); 
            } 
            if (MessageHandler == null)
            {
                throw new InvalidOperationException("Missing MessageReceived handler");
            }

            if (Topic == null)
            {
                throw new InvalidOperationException("Missing Topic");
            }

            foreach (var kv in broker.PoolTable)
            { 
                MqClientPool pool = kv.Value;
                AddConsumeThread(pool);
            }

            broker.ServerJoin += (pool) =>
            {
                AddConsumeThread(pool);
            };
            broker.ServerLeave += (serverAddress) =>
            {
                if (consumeThreadTable.ContainsKey(serverAddress))
                {
                    try
                    {
                        var thread = consumeThreadTable[serverAddress];
                        consumeThreadTable.Remove(serverAddress);
                        thread.Dispose();
                    }
                    catch(Exception e)
                    {
                        log.Error(e); 
                    }
                }
            }; 
        } 
    }
}
