using log4net;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;

namespace zbus
{
    public class Dict : SortedDictionary<string, object>
    {
        public new object this[string key]
        {
            get {
                object value;
                if (!this.TryGetValue(key, out value)) return null;
                return value; 
            }
            set { this.Add(key,value); }
        }
        
    }
    public class Message
    {
        public string Url { get; set; }
        public string Method { get; set; }
        public int? Status { get; set; }
        public Dict Headers = new Dict();
        public object Body { get; set; }

        public void Replace(Message msg)
        {
            this.Url = msg.Url;
            this.Method = msg.Method;
            this.Status = msg.Status;
            this.Headers = msg.Headers;
            this.Body = msg.Body;
        }
    }

    /// <summary> 
    /// </summary> 
    public class WebsocketClient : IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(WebsocketClient)); 
        public int HeartbeatInterval { get; set; } = 30000; //30s
        public int ReconnectDelay { get; set; } = 3000; //3 seconds by default to retry if in disconnected.
        public bool AuthEnabled { get; set; } = false;
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }


        protected readonly string address;
        protected ClientWebSocket socket;

        public event Action<WebsocketClient> OnOpen;
        public event Action<WebsocketClient> OnClose;
        public event Action<Message> OnMessage;

        public Action<Message> BeforeSend;
        public Action<Message> AfterRecv; 

        
        protected IDictionary<string, Action<Message>> callbackTable = new Dictionary<string, Action<Message>>();

        private SemaphoreSlim connectLocker = new SemaphoreSlim(1);
        private SemaphoreSlim readLocker = new SemaphoreSlim(1);
        private SemaphoreSlim writeLocker = new SemaphoreSlim(1); 

        private Thread recvThread; 
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Thread heartbeatThread;
        protected Message heartbeatMessage = null;

        public WebsocketClient(string address)
        {
            if(!address.StartsWith("ws://") && !address.StartsWith("wss://"))
            {
                address = "ws://" + address;
            }
            this.address = address; 
        }
         
        public async Task ConnectAsync(CancellationToken? token = null)
        {
            if (Active) return;
            try
            {
                await connectLocker.WaitAsync();
                if (Active) return;
                this.socket = new ClientWebSocket();
                if(token == null)
                {
                    token = CancellationToken.None;
                }
                await this.socket.ConnectAsync(new Uri(this.address), token.Value);

                //If no error connected event triggered
                logger.Info(string.Format("Connected to {0}", address));
                OnOpen?.Invoke(this);
            }
            catch(Exception e)
            {
                while(e.InnerException != null)
                {
                    e = e.InnerException;
                }
                logger.Error(e);
            }
            finally
            {
                connectLocker.Release();
            } 

            //Trying to start recv thread if not start yet
            if (this.recvThread != null) return;
            try
            {  
                if (this.recvThread != null) return;

                await connectLocker.WaitAsync();
                this.recvThread = new Thread(() =>
                {
                    recvAsync().Wait();
                });
                this.recvThread.IsBackground = false;
                this.recvThread.Start();

                this.Heartbeat();
            }
            finally
            {
                connectLocker.Release();
            }  
        }

        private async Task recvAsync()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    Message res = await RecvAsync(cts.Token);
                    string id = (string)res.Headers[Protocol.ID];
                    if (callbackTable.ContainsKey(id))
                    {
                        var cb = callbackTable[id];
                        callbackTable.Remove(id);
                        try
                        {
                            cb(res);
                        }
                        catch (Exception e)
                        {
                            logger.Error(e.Message, e);
                        }
                        continue;
                    }

                    OnMessage?.Invoke(res);
                }
                catch (Exception e)
                {
                    while (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    if (e is SocketException)
                    {
                        logger.Error(e);
                        CloseConnection();
                        OnClose?.Invoke(this);
                        await Task.Delay(ReconnectDelay);
                    }

                }
            }
        }

        public async Task<Message> InvokeAsync(Message req, int timeoutMillis=10000, Action<Message> beforeSend = null)
        {
            string id = Guid.NewGuid().ToString();
            req.Headers[Protocol.ID] = id;

            ManualResetEvent sync = new ManualResetEvent(false);
            Message res = null;
            callbackTable[id] = (msg) =>
            {
                res = msg;
                sync.Set();
            }; 
            await SendAsync(req, beforeSend);
            sync.WaitOne(timeoutMillis);
            return res; 
        } 

        public async Task SendAsync(Message req, Action<Message> beforeSend = null, CancellationToken ? token = null)
        { 
            if (!Active)
            {
                await ConnectAsync();
            }
            try
            {
                await writeLocker.WaitAsync();
                await SendUnsafeAsync(req, beforeSend, token);
            }
            finally
            {
                writeLocker.Release();
            }
        }
        protected async Task SendUnsafeAsync(Message req, Action<Message> beforeSend = null, CancellationToken ? token = null)
        {
            if (token == null)
            {
                token = CancellationToken.None;
            }
            if (beforeSend == null) beforeSend = BeforeSend;
            if (beforeSend != null)
            {
                beforeSend(req);
            }
            if (AuthEnabled)
            {
                Auth.Sign(ApiKey, SecretKey, req);
            }
            string msg = JsonKit.SerializeObject(req);
            UTF8Encoding encoder = new UTF8Encoding();
            await socket.SendAsync(new ArraySegment<byte>(encoder.GetBytes(msg)),
                WebSocketMessageType.Text, true, token.Value);
        }  


        protected async Task<Message> RecvAsync(CancellationToken? token = null)
        {
            if (!Active)
            {
                await ConnectAsync();
            }
            try
            {
                await readLocker.WaitAsync();
                return await RecvUnsafeAsync(token);
            }
            finally
            {
                readLocker.Release();
            }
        }

        protected async Task<Message> RecvUnsafeAsync(CancellationToken? token = null)
        {
            if (token == null)
            {
                token = CancellationToken.None;
            }
            List<byte> data = new List<byte>(); 
            while (true)
            {
                int bufferSize = 1024 * 64;
                byte[] buffer = new byte[bufferSize];
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None); 
                byte[] recv = new byte[result.Count];
                Array.Copy(buffer, recv, result.Count);
                data.AddRange(recv);
                if (result.EndOfMessage) break; 
            }

            UTF8Encoding encoder = new UTF8Encoding();
            Message res = JsonKit.DeserializeObject<Message>(encoder.GetString(data.ToArray()));
            if (AfterRecv != null)
            {
                AfterRecv(res);
            }
            return res;
        }

        public void Dispose()
        {
            this.cts.Cancel();
            CloseConnection();
        }

        private void CloseConnection()
        { 
            if(socket != null)
            {
                socket.Dispose();
            }
        }

        public bool Active
        {
            get
            {
                return this.socket != null && this.socket.State == WebSocketState.Open;
            }
        } 
       
        public void Heartbeat()
        {
            if (heartbeatMessage == null) return;
            if (this.heartbeatThread != null) return;
            lock (this)
            {
                if (this.heartbeatThread != null) return;

                this.heartbeatThread = new Thread(async () =>
                {
                   while (!cts.IsCancellationRequested)
                   {
                       await Task.Delay(this.HeartbeatInterval);
                       try
                       {
                           if (this.Active)
                           {
                               await SendAsync(heartbeatMessage, BeforeSend);
                           }
                       }
                       catch
                       {
                            //ignore
                       }
                   }
               });
               this.heartbeatThread.Start();
            }
        }  
    }
}
