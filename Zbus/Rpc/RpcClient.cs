using log4net;
using System;
using System.Threading.Tasks;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.Serialization;

namespace zbus
{
    public class RpcClient : MqClient
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RpcClient));
        /// <summary>
        /// URL prefix of the rpc
        /// </summary>
        public string UrlPrefix { get; set; } = "";

        public RpcClient(string address, string urlPrefix="") : base(address)
        {
            UrlPrefix = urlPrefix;
        }   
         
        public async Task<T> InvokeAsync<T>(string url, object[] args, int timeoutMillis = 10000)
        {   
            Message msg = new Message();
            msg.Url = PathKit.Join(UrlPrefix, url);
            msg.Body = args;

            return await InvokeAsync<T>(msg, timeoutMillis);
        }

        public async Task<T> InvokeAsync<T>(Message req, int timeoutMillis = 10000)
        {
            Message res = await base.InvokeAsync(req, timeoutMillis);
            object data = parseResult(res);
            return (T)JsonKit.Convert(data, typeof(T));
        }

        public async Task<object> InvokeAsync(Type type, Message req, int timeoutMillis = 10000)
        {
            Message res = await base.InvokeAsync(req, timeoutMillis);
            object data = parseResult(res);
            return JsonKit.Convert(data, type);
        }

        private object parseResult(Message res)
        { 
            
            if (res.Status != null)
            { 
                if(res.Status != 200) {
                    if (res.Body == null)
                    {
                        throw new RpcException("unknown error");
                    }

                    if (res.Body is Exception)
                    {
                        throw (Exception)res.Body;
                    }
                    throw new RpcException(res.Body.ToString()); 
                } 
            }
            return res.Body;

        }

        public T CreateProxy<T>(string urlPrefix)
        {
            urlPrefix = PathKit.Join(this.UrlPrefix, urlPrefix);
            return new RpcProxy<T>(this, urlPrefix).Create();
        }
    }


    public class RpcProxy<T> : RealProxy
    {
        private RpcClient rpcClient;
        private string urlPrefix;
        public RpcProxy(RpcClient rpcClient, string urlPrefix) : base(typeof(T))
        {
            this.rpcClient = rpcClient;
            this.urlPrefix = urlPrefix;
        }

        public T Create()
        {
            return (T)GetTransparentProxy();
        }

        public dynamic Request(Type realReturnType, Message request)
        {
            dynamic resp = rpcClient.InvokeAsync(realReturnType, request).Result;
            if (realReturnType == typeof(void)) return null;

            if (realReturnType != resp.GetType() && !typeof(Task).IsAssignableFrom(realReturnType))
            {
                return JsonKit.Convert(resp, realReturnType);
            }
            return resp;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = (IMethodCallMessage)msg;
            var method = (MethodInfo)methodCall.MethodBase;
            if (method.DeclaringType.FullName.Equals("System.IDisposable"))
            {
                return new ReturnMessage(null, null, 0, methodCall.LogicalCallContext, methodCall);
            }
            if (method.DeclaringType.Name.Equals("Object"))
            {
                var result = method.Invoke(this, methodCall.Args);
                return new ReturnMessage(result, null, 0, methodCall.LogicalCallContext, methodCall);
            }

            try
            {
                string methodName = methodCall.MethodName;
                object[] args = methodCall.Args;  
                Message req = new Message();
                req.Url = PathKit.Join(this.urlPrefix, methodName);
                req.Body = args;

                Type returnType = method.ReturnType;

                //Simple methods
                if (!typeof(Task).IsAssignableFrom(returnType))
                {
                    dynamic res = Request(returnType, req);
                    if (res != null && res is Exception)
                    {
                        return new ReturnMessage(res as Exception, methodCall);
                    }
                    return new ReturnMessage(res, null, 0, methodCall.LogicalCallContext, methodCall);
                }

                //Task returned method 
                Type realType = typeof(void);
                if (returnType.GenericTypeArguments.Length >= 1)
                {
                    realType = returnType.GenericTypeArguments[0];
                }

                Task task = null;
                if (realType == typeof(void))
                {
                    task = Task.Run(() =>
                    {
                        Request(realType, req);
                    });
                }
                else
                {
                    MethodInfo invokeMethod = this.GetType().GetRuntimeMethod("Request", new Type[] { typeof(Type), typeof(Message) });

                    var calledExp = Expression.Call(
                       Expression.Constant(this),
                       invokeMethod,
                       Expression.Constant(realType),
                       Expression.Constant(req)
                    );

                    var castedExp = Expression.Convert(calledExp, realType);

                    var d = Expression.Lambda(castedExp).Compile();
                    task = (Task)Activator.CreateInstance(returnType, d);
                    task.Start();
                }

                return new ReturnMessage(task, null, 0, methodCall.LogicalCallContext, methodCall);

            }
            catch (Exception e)
            {
                if (e is TargetInvocationException && e.InnerException != null)
                {
                    return new ReturnMessage(e.InnerException, msg as IMethodCallMessage);
                }
                return new ReturnMessage(e, msg as IMethodCallMessage);
            }
        }
    }

    public class RpcException : Exception
    {
        public RpcException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }

        public RpcException()
        {
        }

        public RpcException(string message)
            : base(message)
        {
        }
        public RpcException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
