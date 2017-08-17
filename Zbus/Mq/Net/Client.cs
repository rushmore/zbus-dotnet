using log4net; 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Mq.Net
{

    /// <summary>
    /// Identity interface to track message match for asynchroneous invocation.
    /// </summary>
    public interface Id
    {
        /// <summary>
        /// Identity string
        /// </summary>
        string Id { get; set; }
    }

    /// <summary>
    /// TCP client, capable of send/recv message, and message codec is plugable,
    /// SendAsync/RecvAsync/InvokeAsync all conform to C# async api.
    /// 
    /// Client no need to connect explicitly, it is called internally by methods like SendXxx/RecvXxx/InokeXxx
    /// 
    /// Client works in 2 different scenarios: 
    /// 1) Auto matching, SendAsync/RecvAsync/InvokeAsync with message matched by ID
    /// 2) Daemon, Start a thread, monitor message by MessageReceived + SendAsync
    /// 
    /// Heartbeat:
    /// By default a created Client starts one thread to do hearbeat every configurable interval of time,
    /// and the default heartbeat interval is 30 seconds, which of course should be less than the server's
    /// idle checking time, for example zbus is configured by default to be 60 seconds idle checking.
    /// 
    /// Thread safety:
    /// All public methods are thread safe.
    /// 
    /// </summary>
    /// <typeparam name="REQ">Request message type</typeparam>
    /// <typeparam name="RES">Response message type</typeparam>
    public class Client<REQ, RES> : IDisposable where REQ : Id where RES : Id
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Client<REQ, RES>));
        public bool AllowSelfSignedCertficate { get; set; }
        public int HeartbeatInterval { get; set; } = 60000; //1 minute
        public int RetryInterval { get; set; } = 3000; //3 seconds by default to retry if in disconnected.

        private ICodec codecRead;
        private ICodec codecWrite;

        private ServerAddress serverAddress;
        private string certFile;

        private ByteBuffer readBuf = new ByteBuffer();
        private IDictionary<string, RES> resultTable = new ConcurrentDictionary<string, RES>();

        private TcpClient tcpClient;
        private Stream stream;
        private SemaphoreSlim connectLocker = new SemaphoreSlim(1);
        private SemaphoreSlim readLocker = new SemaphoreSlim(1);
        private SemaphoreSlim writeLocker = new SemaphoreSlim(1);

        public Client(ServerAddress serverAddress, ICodec codecRead, ICodec codecWrite, string certFile = null)
        {
            this.serverAddress = serverAddress;
            this.codecRead = codecRead;
            this.codecWrite = codecWrite;
            this.certFile = certFile;

            this.StartHeartbeat();
        }
        public Client(string serverAddress, ICodec codecRead, ICodec codecWrite) :
            this(new ServerAddress(serverAddress), codecRead, codecWrite)
        {

        }
        public Client(string serverAddress, ICodec codec)
            : this(serverAddress, codec, codec)
        {
        } 

        public async Task ConnectAsync()
        {
            if (Active) return;
            try
            {
                await connectLocker.WaitAsync(); 
                if (Active) return;
                await ConnectUnsafeAsync();
            }
            finally
            {
                connectLocker.Release();
            }
            //If no error connected event triggered
            Connected?.Invoke(); 
        } 

        public async Task<RES> InvokeAsync(REQ req, CancellationToken? token = null)
        {
            if (!Active)
            {
                await ConnectAsync();
            }

            await SendAsync(req, token);
            string reqId = req.Id;
            RES res;
            while (true)
            {
                if (resultTable.ContainsKey(reqId))
                {
                    res = resultTable[reqId];
                    resultTable.Remove(reqId);
                    return res;
                }

                res = await RecvAsync(token);
                if (res.Id == reqId) return res; 
                resultTable[res.Id] = res;
            } 
        }

        public async Task SendAsync(REQ req, CancellationToken? token = null)
        {
            if (!Active)
            {
                await ConnectAsync();
            }
            try
            {
                await writeLocker.WaitAsync(); 
                await SendUnsafeAsync(req, token);
            }
            finally
            {
                writeLocker.Release();
            }
        }
        public async Task<RES> RecvAsync(CancellationToken? token = null)
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

        protected async Task ConnectUnsafeAsync()
        {
            if (Active) return;
            string[] bb = this.serverAddress.Address.Trim().Split(':');
            string host;
            int port = 80;
            if (bb.Length < 2)
            {
                host = bb[0];
            }
            else
            {
                host = bb[0];
                port = int.Parse(bb[1]);
            }
            this.tcpClient = new TcpClient();
            this.tcpClient.NoDelay = true;
            log.Debug("Trying connect to " + serverAddress);
            await this.tcpClient.ConnectAsync(host, port);
            log.Debug("Connected to " + serverAddress);

            this.stream = this.tcpClient.GetStream();

            if (this.serverAddress.SslEnabled)
            {
                if (this.certFile == null)
                {
                    throw new ArgumentException("Missing certificate file");
                }
                X509Certificate.CreateFromCertFile(this.certFile);
                SslStream sslStream = new SslStream(this.stream, false,
                    new RemoteCertificateValidationCallback((object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                    {
                        if (AllowSelfSignedCertficate) return true;
                        if (sslPolicyErrors == SslPolicyErrors.None)
                        {
                            return true;
                        }
                        Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
                        return false;
                    }),
                    null);

                sslStream.AuthenticateAsClient(this.serverAddress.Address);
                this.stream = sslStream;
            }
        }

        protected async Task SendUnsafeAsync(REQ req, CancellationToken? token = null)
        { 
            if (token == null)
            {
                token = CancellationToken.None;
            }
            if (req.Id == null)
            {
                req.Id = Guid.NewGuid().ToString();
            } 
            ByteBuffer buf = this.codecWrite.Encode(req);
            await stream.WriteAsync(buf.Data, 0, buf.Limit, token.Value).ConfigureAwait(false);
            await stream.FlushAsync(token.Value).ConfigureAwait(false);
        }


        protected async Task<RES> RecvUnsafeAsync(CancellationToken? token = null)
        { 
            if (token == null)
            {
                token = CancellationToken.None;
            }
            byte[] buf = new byte[4096];
            while (true)
            {
                ByteBuffer tempBuf = this.readBuf.Duplicate();
                tempBuf.Flip(); //to read mode
                object msg = codecRead.Decode(tempBuf);
                if (msg != null)
                {
                    this.readBuf.Move(tempBuf.Position);
                    RES res = (RES)msg;  
                    return res;
                }
                int n = await stream.ReadAsync(buf, 0, buf.Length, token.Value).ConfigureAwait(false);
                if (n <= 0)
                {
                    throw new IOException("End of stream, probably closed by remote server");
                }
                this.readBuf.Put(buf, 0, n);
            }
        } 

        public void Dispose()
        {
            this.cts.Cancel();
            CloseConnection();
        }

        private void CloseConnection()
        {
            if (stream != null)
            {
                stream.Close();
            }
            if (this.tcpClient != null)
            {
                this.tcpClient.Close();
            } 
        }

        public bool Active
        {
            get
            {
                return this.tcpClient != null && this.tcpClient.Client != null && this.tcpClient.Connected;
            }
        }

        #region Heartbeat 
        protected virtual async Task DoHeartbeat()
        {
            log.Debug("Sending heartbeat...");
            await Task.FromResult(false);
        }

        private Thread heartbeatThread;
        public void StartHeartbeat()
        {
            if (this.heartbeatThread != null) return;
            lock (this)
            {
                if (this.heartbeatThread != null) return;

                this.heartbeatThread = new Thread( async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        await Task.Delay(this.HeartbeatInterval);
                        try
                        {
                            if (this.Active)
                            {
                                await DoHeartbeat();
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
        #endregion

        public event Action Connected;
        public event Action Disconnected;
        public event Action<RES> MessageReceived;
        private Thread recvThread; 
        private CancellationTokenSource cts = new CancellationTokenSource();  

        public void Start()
        {
            if (this.recvThread != null) return;
            lock (this)
            {
                if (this.recvThread != null) return;

                this.recvThread = new Thread(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        try
                        {
                            RES res = await RecvAsync(cts.Token);
                            MessageReceived?.Invoke(res);
                        }
                        catch (Exception e)
                        {
                            if (e is SocketException || e is IOException)
                            {
                                log.Error(e);
                                CloseConnection();
                                Disconnected?.Invoke();
                                await Task.Delay(RetryInterval);
                            }
                            
                        }
                    }
                });
                this.recvThread.Start();
            } 
        } 

    }


    public class Client<T> : Client<T, T> where T : Id
    {
        public Client(string serverAddress, ICodec codec)
            : base(serverAddress, codec) { }
        public Client(ServerAddress serverAddress, ICodec codec, string certFile = null)
            : base(serverAddress, codec, codec, certFile) { }
    }
}
