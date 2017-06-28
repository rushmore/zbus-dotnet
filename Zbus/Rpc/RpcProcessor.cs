using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Zbus.Mq;
namespace Zbus.Rpc
{

    public class RpcProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RpcProcessor));

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        private Dictionary<string, MethodInstance> methods = new Dictionary<string, MethodInstance>();

        public void AddModule<T>(string module = null)
        {
            Type type = typeof(T);
            object instance = Activator.CreateInstance(type);
            if (module == null)
            {
                AddModule(instance);
            }
            else
            {
                AddModule(module, instance);
            }
        }

        public void AddModule<T>()
        {
            AddModule(typeof(T)); 
        }

        public void AddModule(Type t)
        { 
            object instance = t.GetConstructors()[0].Invoke(new object[0]);
            AddModule(instance);
        }

        public void AddModule(object service)
        {
            foreach (Type type in service.GetType().GetInterfaces())
            {
                AddModule(type.Name, service);
                AddModule(type.FullName, service);
            }

            AddModule("", service);
            AddModule(service.GetType().Name, service);
            AddModule(service.GetType().FullName, service);
        }

        public void AddModule(string module, object service)
        {
            BuildMethodTable(methods, module, service); 
        } 

        private void BuildMethodTable(IDictionary<string, MethodInstance> table, string module, object service)
        { 
            IDictionary<string, MethodInstance> ignore = new Dictionary<string, MethodInstance>();
            List<Type> types = new List<Type>();
            types.Add(service.GetType());
            foreach (Type type in service.GetType().GetInterfaces())
            {
                types.Add(type);
            }
            foreach (Type type in types)
            {
                foreach (MethodInfo info in type.GetMethods())
                {
                    bool exclude = false;
                    string id = info.Name;
                    if (info.DeclaringType != type || !info.IsPublic) continue;

                    foreach (Attribute attr in Attribute.GetCustomAttributes(info))
                    {
                        if (attr.GetType() == typeof(Remote))
                        {
                            Remote r = (Remote)attr;
                            if (r.Id != null)
                            {
                                id = r.Id;
                            }
                            if (r.Exclude)
                            {
                                exclude = true;
                            }
                            break;
                        }
                    }
                    ParameterInfo[] paramInfo = info.GetParameters();
                    Type[] paramTypes = new Type[paramInfo.Length];
                    for(int i = 0; i < paramTypes.Length; i++)
                    {
                        paramTypes[i] = paramInfo[i].ParameterType;
                    }
                    IList<string> keys = Keys(module, id, paramTypes);

                    MethodInstance instance = new MethodInstance(info, service);
                    foreach(string key in keys)
                    {
                        table[key] = instance;
                    } 
                    if (exclude)
                    {
                        foreach (string key in keys)
                        {
                            ignore[key] = instance;
                        }
                    } 
                }
            }
            foreach (string key in ignore.Keys)
            {
                table.Remove(key);
            } 
        } 
        
         
        public void MessageHandler(Message msg, MqClient client)
        {
            Message msgRes = new Message
            {
                Status = 200,
                Recver = msg.Sender,
                Id = msg.Id
            };

            Response response = null;
            try
            {
                string encodingName = msg.Encoding;
                Encoding encoding = this.Encoding;
                if (encodingName != null)
                {
                    encoding = Encoding.GetEncoding(encodingName);
                }

                Request request = JsonKit.DeserializeObject<Request>(msg.GetBody(encoding));
                response = ProcessAsync(request).Result;
            }
            catch (Exception e)
            {
                response = new Response
                {
                    Error = e
                };
            }
             
            try
            {
                msgRes.SetJsonBody(JsonKit.SerializeObject(response), this.Encoding); 
                Task task = client.RouteAsync(msgRes);
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        public async Task<Response> ProcessAsync(Request request)
        {
            Response response = new Response();
            string module = request.Module == null ? "" : request.Module;
            string method = request.Method;
            object[] args = request.Params;

            MethodInstance target = null;
            if (request.Method == null)
            {
                response.Error = new RpcException("missing method name");
                return response;
            }

            target = FindMethod(module, method, args);
            if (target == null)
            {
                string errorMsg = method + " not found";
                if (module != "")
                {
                    errorMsg = module + ":" + errorMsg;
                }
                response.Error = new RpcException(errorMsg);
                return response;
            } 

            try
            {
                ParameterInfo[] pinfo = target.Method.GetParameters();
                if (pinfo.Length != args.Length)
                {
                    response.Error = new RpcException("number of argument not match");
                    return response;
                }
                for (int i = 0; i < pinfo.Length; i++)
                {
                    if (args[i].GetType() != pinfo[i].ParameterType)
                    {
                        args[i] = JsonKit.Convert(args[i], pinfo[i].ParameterType);
                    }
                }

                dynamic invoked = target.Method.Invoke(target.Instance, args);
                if (invoked != null && typeof(Task).IsAssignableFrom(invoked.GetType()))
                {
                    if (target.Method.ReturnType.GenericTypeArguments.Length > 0)
                    {
                        response.Result = await invoked;
                    }
                    else
                    {
                        response.Result = null;
                    }
                }
                else
                {
                    response.Result = invoked;
                }
                return response;
            }
            catch (Exception ex)
            {
                response.Error = ex;
                if (ex.InnerException != null)
                {
                    response.Error = ex.InnerException;
                }
                return response;
            }
        }

        private MethodInstance FindMethod(string module, string method, object[] args)
        {
            Type[] types = null;
            if (args != null)
            {
                types = new Type[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    types[i] = args[i].GetType();
                }
            }

            IList<string> keys = Keys(module, method, types);
            foreach (string key in keys)
            {
                if (this.methods.ContainsKey(key))
                {
                    return this.methods[key];
                }
            }
            return null;
        } 

        private IList<string> Keys(string module, string method, Type[] types)
        {
            string paramMD5 = null, key;
            if(types != null)
            {
                foreach (Type type in types)
                {
                    paramMD5 += type + ",";
                } 
            } 

            IList<string> keys = new List<string>();
            key = module + ":" + method;
            if (paramMD5 != null)
            {
                key += ":" + paramMD5;
            }

            if (!keys.Contains(key))
            {
                keys.Add(key);
            }

            key = module + ":" + char.ToUpper(method[0]) + method.Substring(1);
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }

            key = module + ":" + char.ToLower(method[0]) + method.Substring(1);
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }

            key = module + ":" + char.ToUpper(method[0]) + method.Substring(1);
            if (paramMD5 != null)
            {
                key += ":" + paramMD5;
            }
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }

            key = module + ":" + char.ToLower(method[0]) + method.Substring(1);
            if (paramMD5 != null)
            {
                key += ":" + paramMD5;
            }
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }

            string async = "Async";
            if (method.EndsWith(async)) //special for Async method
            {
                key = module + ":" + method.Substring(0, method.Length - async.Length);
                if (paramMD5 != null)
                {
                    key += ":" + paramMD5;
                }
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        private class MethodInstance
        {
            public MethodInfo Method { get; set; }
            public object Instance { get; set; }

            public MethodInstance(MethodInfo method, object instance)
            {
                this.Method = method;
                this.Instance = instance;
            }
        }
    }

    public class Remote : Attribute
    {
        public string Id { get; set; }
        public bool Exclude { get; set; }

        public Remote()
        {
            Id = null;
        }

        public Remote(string id)
        {
            this.Id = id;
        }

        public Remote(bool exclude)
        {
            this.Exclude = exclude;
        }
    }
}