using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zbus.Mq
{
    public delegate ServerAddress[] ServerSelector(BrokerRouteTable routeTable, Message msg);
    public class Broker : IDisposable
    { 
        private static readonly ILog log = LogManager.GetLogger(typeof(Broker));

        public int? ClientPoolSize { get; set; }
        public string DefaultSslCertFile { get; set; } 
        public event Action<MqClientPool> ServerJoin;
        public event Action<ServerAddress> ServerLeave;
        public BrokerRouteTable RouteTable { get; private set; }   
        public IDictionary<ServerAddress, MqClientPool> PoolTable { get; private set; }

        private IDictionary<ServerAddress, MqClient> trackerSubscribers = new ConcurrentDictionary<ServerAddress, MqClient>();

        private IDictionary<string, string> sslCertFileTable = new ConcurrentDictionary<string, string>();

        public Broker(string trackerServerList=null, int clientPoolSize=32)
        {
            RouteTable = new BrokerRouteTable();
            PoolTable = new ConcurrentDictionary<ServerAddress, MqClientPool>();
            ClientPoolSize = clientPoolSize;
            if(trackerServerList != null)
            {
                string[] bb = trackerServerList.Split(new char[] { ';', ',', ' ' });
                foreach(string trackerAddress in bb)
                {
                    AddTracker(trackerAddress);
                }
            }
        }


        public MqClientPool[] Select(ServerSelector selector, Message msg)
        {
            ServerAddress[] addressArray = selector(RouteTable, msg);
            if (addressArray == null) return new MqClientPool[0];

            MqClientPool[] res = new MqClientPool[addressArray.Length];
            bool shrink = false;
            for(int i=0;i < addressArray.Length; i++)
            {
                ServerAddress address = addressArray[i];
                if (PoolTable.ContainsKey(address))
                {
                    res[i] = PoolTable[address];
                } 
                else
                {
                    res[i] = null;
                    shrink = true;
                }
            }
            if (!shrink) return res;

            return res.Where<MqClientPool>(e => e != null).ToArray<MqClientPool>(); 
        }

        public void AddTracker(ServerAddress trackerAddress, string certFile = null, int waitTime=3000)
        {
            MqClient client = new MqClient(trackerAddress, certFile);
            trackerSubscribers[trackerAddress] = client;
            CountdownEvent countDown = new CountdownEvent(1);
            bool firstTime = true;
            client.Connected += async () =>
            {
                Message msg = new Message
                {
                    Cmd = Protocol.TRACK_SUB,
                };
                await client.SendAsync(msg);
            };
            client.MessageReceived += (msg) =>
            { 
                if(msg.Status != 200)
                {
                    log.Error(msg.BodyString);
                    return;
                }
                TrackerInfo trackerInfo = JsonKit.DeserializeObject<TrackerInfo>(msg.BodyString);

                IList<ServerAddress> toRemove = RouteTable.UpdateTracker(trackerInfo);
                foreach(ServerInfo serverInfo in RouteTable.ServerTable.Values)
                {
                    AddServer(serverInfo);
                }
                foreach(ServerAddress serverAddress in toRemove)
                {
                    RemoveServer(serverAddress);
                }
                if (firstTime)
                {
                    countDown.Signal();
                    firstTime = false;
                } 
            };
            client.Start(); 
            countDown.Wait(waitTime);
            countDown.Dispose();
        }
        public void AddTracker(string trackerAddress, string certFile = null)
        {
            AddTracker(new ServerAddress(trackerAddress), certFile);
        }

        private void AddServer(ServerInfo serverInfo)
        { 
            ServerAddress serverAddress = serverInfo.ServerAddress;
            if (PoolTable.ContainsKey(serverAddress)) return;

            string certFile = GetCertFile(serverAddress);

            MqClientPool pool = new MqClientPool(serverAddress, certFile);
            if (ClientPoolSize.HasValue)
            {
                pool.MaxCount = ClientPoolSize.Value;
            } 
            PoolTable[pool.ServerAddress] = pool;
            ServerJoin?.Invoke(pool);
        }

        private void RemoveServer(ServerAddress serverAddress)
        {
            MqClientPool pool;
            PoolTable.TryGetValue(serverAddress, out pool);
            if(pool != null)
            {
                PoolTable.Remove(serverAddress); 
                ServerLeave?.Invoke(serverAddress); 
                pool.Dispose(); 
            }
        } 


        private string GetCertFile(ServerAddress serverAddress, string certFile = null)
        {
            if (certFile != null)
            {
                sslCertFileTable[serverAddress.Address] = certFile;
                return certFile;
            }
            if (sslCertFileTable.ContainsKey(serverAddress.Address))
            {
                return sslCertFileTable[serverAddress.Address];
            }
            return DefaultSslCertFile;
        }

        public void Dispose()
        {
            foreach(var kv in trackerSubscribers)
            {
                kv.Value.Dispose();
            }
            trackerSubscribers.Clear();
            foreach(var kv in PoolTable)
            {
                kv.Value.Dispose();
            }
            PoolTable.Clear();
        }
    }
}
