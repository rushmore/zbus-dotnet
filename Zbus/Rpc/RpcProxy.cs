using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Rpc
{ 
    public class RpcProxy<T> : RealProxy
    {  
        private RpcInvoker rpcInvoker;

        public RpcProxy(RpcInvoker rpcInvoker) : base(typeof(T))
        {
            this.rpcInvoker = rpcInvoker;
        } 

        public T Create()
        {
            return (T)GetTransparentProxy();
        }

        protected Response Invoke(Type realReturnType, Request request)
        {
            Response res = rpcInvoker.InvokeAsync(request).Result; 
            if (res.Result == null) return res;
            if (realReturnType == typeof(void)) return res;

            if (realReturnType != res.Result.GetType() && !typeof(Task).IsAssignableFrom(realReturnType))
            {
                res.Result = JsonKit.Convert(res.Result, realReturnType);
            }
            return res;
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

                Request req = new Request
                {
                    Method = methodName,
                    Params = args,
                    Module = rpcInvoker.Module,
                };

                Type returnType = method.ReturnType;

                //Simple methods
                if (!typeof(Task).IsAssignableFrom(returnType))
                {
                    Response resp = Invoke(returnType, req);

                    if (resp.Error != null)
                    {
                        Exception error = null;
                        if (resp.Error is Exception)
                        {
                            error = (Exception)resp.Error;
                        }
                        else
                        {
                            error = new RpcException(resp.Error.ToString());
                        } 
                        return new ReturnMessage(error, methodCall);
                    }
                    return new ReturnMessage(resp.Result, null, 0, methodCall.LogicalCallContext, methodCall);
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
                        Invoke(realType, req);
                    });
                }
                else
                {
                    MethodInfo invokeMethod = this.GetType().GetRuntimeMethod("Request", new Type[] { typeof(Type), typeof(Request) });

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
}