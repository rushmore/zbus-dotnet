using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zbus.Mq
{
    public class ConsumeThread : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConsumeThread));

        public Func<MqClient> ClientFactory { get; set; }
        public event Action<Message, MqClient> MessageRecevied;
        public int ConnectionCount { get; set; }
        public bool RunInPool { get; set; }
        public int ConsumeTimeout { get; set; }
        public int? ConsumeWindow { get; set; } 

        public string Topic { get; private set; }
        public string Token { get; set; }
        public ConsumeGroup ConsumeGroup { get; private set; } 
         

        private CancellationTokenSource cts = new CancellationTokenSource(); 
        private Thread[] consumeThreadList;
        private MqClient[] clients;

        public ConsumeThread(string topic, ConsumeGroup group = null)
        {
            ConsumeTimeout = 10000; //10s 
            Topic = topic;
            ConsumeGroup = group==null? new ConsumeGroup(topic): group;
            RunInPool = false;
            ConnectionCount = 1; 
        }

        private async Task<Message> TakeAsync(MqClient client, CancellationToken? token=null)
        {
            Message res = await client.ConsumeAsync(Topic, ConsumeGroup.GroupName, ConsumeWindow, token);
            if (res == null) return res;
            if(res.Status == 404)
            {
                await client.DeclareGroupAsync(Topic, ConsumeGroup, token);
                return await TakeAsync(client, token);
            }

            if(res.Status == 200)
            {
                res.Id = res.OriginId;
                res.RemoveHeader(Protocol.ORIGIN_ID);
                if (res.OriginUrl != null)
                {
                    res.Url = res.OriginUrl;
                    res.Status = null;
                    res.RemoveHeader(Protocol.ORIGIN_URL);
                }
                return res;
            }
            throw new MqException(res.BodyString); 
        }

        public void Start()
        {
            if (MessageRecevied == null)
            {
                throw new InvalidOperationException("Missing MessageReceived handler");
            }
            if (ClientFactory == null)
            {
                throw new InvalidOperationException("Missing ClientFactory");
            }

            lock (this)
            {
                if (this.consumeThreadList != null) return;
            }
            this.consumeThreadList = new Thread[ConnectionCount];
            this.clients = new MqClient[ConnectionCount];

            for(int i = 0; i < this.consumeThreadList.Length; i++)
            {
                MqClient client = this.clients[i] = ClientFactory();
                this.consumeThreadList[i] = new Thread( () =>
                {
                    using (client) { 
                        while (!cts.IsCancellationRequested)
                        {
                            Message msg;
                            try
                            {
                                msg =  TakeAsync(client, cts.Token).Result;
                                if (msg == null) continue;

                                if (RunInPool)
                                {
                                    Task.Run(() =>
                                    {
                                        MessageRecevied?.Invoke(msg, client);
                                    });
                                }
                                else
                                {
                                    MessageRecevied?.Invoke(msg, client);
                                }
                            } 
                            catch (Exception e)
                            { 
                                if (e is SocketException || e is IOException)
                                {
                                    client.Dispose();
                                    Thread.Sleep(3000);
                                }  
                                log.Error(e);
                            }
                        }
                    }
                });
            } 
            foreach(Thread thread in this.consumeThreadList)
            {
                thread.Start();
            }
        }

        public void Dispose()
        {  
            cts.Cancel();
            for (int i = 0; i < this.clients.Length; i++)
            {
                try
                {
                    this.clients[i].Dispose();
                }
                catch(Exception e)
                {
                    log.Error(e.Message, e);
                    //ignore
                }
            }
        } 
    } 
}
