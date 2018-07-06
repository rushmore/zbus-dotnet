using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace zbus
{

    public class PathKit
    {
        public static string Join(params string[] paths)
        {
            string p = "";
            foreach(string path in paths)
            {
                p += "/" + path;
            }
            Regex rgx = new Regex("[/]+");
            string result = rgx.Replace(p, "/");
            if(result.Length > 1 && result.EndsWith("/"))
            {
                result = result.Substring(0, result.Length - 1);
            }
            return result;
        }
    }

    public class UrlEntry
    {
        public string Url { get; set; }
        public string Mq { get; set; }
    }

    public class RpcProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RpcProcessor));

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public string UrlPrefix { get; set; } = "";

        public string DocUrlPrefix { get; set; } = "doc";

        public bool DocEnabled { get; set; } = true;

        public Dictionary<string, MethodInstance> UrlPath2Method { get; } = new Dictionary<string, MethodInstance>();

        public void Mount(string urlPrefix, object service, bool docEnabled = true)
        {  
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
                    if (info.DeclaringType != type || !info.IsPublic) continue;

                    string urlPath = PathKit.Join(urlPrefix, info.Name);
                    bool exclude = false;
                    string httpMethod = null;
                    foreach (Attribute attr in Attribute.GetCustomAttributes(info))
                    {
                        if (attr.GetType() == typeof(RequestMapping))
                        {
                            RequestMapping r = (RequestMapping)attr;
                            if (r.Path != null)
                            {
                                urlPath = PathKit.Join(urlPrefix, r.Path);
                            }
                            if (r.Exclude)
                            {
                                exclude = true;
                            }
                            if(r.Method != null)
                            {
                                httpMethod = r.Method;
                            }

                            break;
                        }
                    }
                    if (exclude) continue;
                    ParameterInfo[] paramInfo = info.GetParameters();
                    Type[] paramTypes = new Type[paramInfo.Length];
                    for(int i = 0; i < paramTypes.Length; i++)
                    {
                        paramTypes[i] = paramInfo[i].ParameterType;
                    }  
                    MethodInstance instance = new MethodInstance(info, service);
                    instance.HttpMethod = httpMethod;
                    instance.DocEnabled = docEnabled;
                    UrlPath2Method[urlPath] = instance; 
                }
            } 

        } 

        public void MountDoc()
        {
            if (!DocEnabled) return;
            string urlPath = PathKit.Join(DocUrlPrefix, "/");
            if (UrlPath2Method.ContainsKey(urlPath)) return;

            this.Mount(DocUrlPrefix, new RpcInfo(this), false);
        }
        
        public List<UrlEntry> UrlEntryList(string mq)
        {
            List<UrlEntry> res = new List<UrlEntry>();

            foreach (var kv in UrlPath2Method)
            {
                string urlPath = kv.Key; 
                var e = new UrlEntry
                {
                    Url = PathKit.Join(UrlPrefix, urlPath),
                    Mq = mq
                };
                res.Add(e);
            }
            return res; 
        }

        private void reply(Message response, int status, string message)
        {
            response.Status = status;
            response.Headers["content-type"] = "text/plain; charset=utf8";
            response.Body = message;
        }

        private object[] parseParam(string url)
        {
            List<object> args = new List<object>();
            int idx = url.IndexOf("?");
            string path = url;
            string argsStr = null;
            if(idx >= 0)
            {
                path = url.Substring(0, idx);
                argsStr = url.Substring(idx + 1);
            }
            string[] bb = path.Split('/');
            foreach(string b in bb)
            {
                if (b.Length == 0) continue;
                args.Add(b);
            }
            if(argsStr != null)
            {
                IDictionary<string, string> dict = new Dictionary<string, string>();
                bb = argsStr.Split('&');
                foreach(string b in bb)
                {
                    if(b.Length == 0) continue;
                    string[] kv = b.Split('=');
                    if (kv.Length != 2) continue;
                    dict[kv[0].Trim()] = dict[kv[1].Trim()];
                }
                if(dict.Count > 0)
                {
                    args.Add(dict);
                }
            }
            return args.ToArray(); 
        }

        private bool checkParams(Message req, Message res, MethodInfo method, object[] args, object[] invokeArgs)
        {
            ParameterInfo[] pinfo = method.GetParameters();
            int count = 0;
            foreach (ParameterInfo info in pinfo)
            {
                if (typeof(Message).IsAssignableFrom(info.ParameterType))
                {
                    continue;
                }
                count++;
            }
            if(count != args.Length)
            {
                reply(res, 400, string.Format("Request(Url={0}, Method={1}, Params={2}) Bad Format", req.Url, method.Name, JsonKit.SerializeObject(args)));
                return false;
            }
            int j = 0;
            for (int i = 0; i < pinfo.Length; i++)
            {
                if (typeof(Message).IsAssignableFrom(pinfo[i].ParameterType))
                {
                    invokeArgs[i] = req;
                    continue;
                }
                invokeArgs[i] = JsonKit.Convert(args[j++], pinfo[i].ParameterType);  
            } 

            return true;
        }

        public async Task ProcessAsync(Message request, Message response)
        {
            response.Status = 200;
            if(request.Url == null)
            {
                reply(response, 400, "Missing url in request");
                return;
            }
            string url = request.Url;
            int length = 0;
            MethodInstance target = null;
            string targetPath = null;
            foreach(var e in this.UrlPath2Method)
            {
                string path = e.Key;
                if (url.StartsWith(path))
                {
                    if(path.Length > length)
                    {
                        target = e.Value;
                        targetPath = path;
                        length = path.Length;
                    }
                }
            }
            if (target == null)
            {
                reply(response, 404, string.Format("Url={0} Not Found", url));
                return;
            }
            object[] args = new object[0];
            if(request.Body != null)
            {
                args = JsonKit.Convert<object[]>(request.Body);
            }
            else
            {
                args = parseParam(url.Substring(targetPath.Length));
            } 
             
            ParameterInfo[] pinfo = target.Method.GetParameters();  
            object[] invokeArgs = new object[pinfo.Length];
            bool ok = checkParams(request, response, target.Method, args, invokeArgs);
            if (!ok) return;

            dynamic invoked = target.Method.Invoke(target.Instance, invokeArgs);
            if (invoked != null && typeof(Task).IsAssignableFrom(invoked.GetType()))
            {
                if (target.Method.ReturnType.GenericTypeArguments.Length > 0)
                {
                    invoked = await invoked;
                } 
            }

            if(invoked is Message)
            {
                response.Replace((Message)invoked);
            }
            else
            {
                response.Body = invoked;
                response.Headers["content-type"] = "application/json; charset=utf8;";
            } 

        } 

        public class MethodInstance
        {
            public MethodInfo Method { get; set; }
            public object Instance { get; set; }

            public string HttpMethod { get; set; }
            public bool DocEnabled { get; set; } = true;

            public MethodInstance(MethodInfo method, object instance)
            {
                this.Method = method;
                this.Instance = instance;
            }
        }
    }

    public class RequestMapping : Attribute
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public bool Exclude { get; set; }

        public RequestMapping(string path=null, string method=null, bool exclude=false)
        {
            Path = path;
            Method = method;
            Exclude = exclude;
        } 
    }


    public class RpcInfo
    {
        private RpcProcessor rpcProcessor; 
        public RpcInfo(RpcProcessor rpcProcessor)
        {
            this.rpcProcessor = rpcProcessor; 
        }

        [RequestMapping("/")]
        public Message Index()
        {
            Message res = new Message();
            res.Status = 200;
            res.Headers["content-type"] = "text/html; charset=utf8"; 
            res.Body = BuildRpcInfo();

            return res;
        }
         

        private string BuildRpcInfo()
        {
            string info = ""; 
            foreach(var kv in rpcProcessor.UrlPath2Method)
            {
                string urlPath = kv.Key;
                var m = kv.Value;
                if (m.DocEnabled == false) continue;

                string returnType = m.Method.ReturnType.ToString();
                var args = "";
                ParameterInfo[] pinfos = m.Method.GetParameters();
                for (int i = 0; i < pinfos.Length; i++)
                {
                    ParameterInfo pinfo = pinfos[i];
                    args += pinfo.ToString();
                    if (i < pinfos.Length - 1)
                    {
                        args += ", ";
                    }
                }
                var link = PathKit.Join(rpcProcessor.UrlPrefix, urlPath);
                info += string.Format(RpcMethodTemplate, link, returnType, m.Method.Name, args);
            }  
            return string.Format(RpcInfoTemplate, rpcProcessor.UrlPrefix, RpcStyleTemplate, info); 
        }


        public static readonly string RpcInfoTemplate = @"
<html><head>
<meta http-equiv=""Content-type"" content=""text/html; charset=utf-8"">
<title>{0} C#</title>
{1}

<script>  
var rpc; 
function init(){{
    rpc = new RpcClient(null,'{0}'); 
}}
</script> 
<script async src=""https://unpkg.com/zbus/zbus.min.js"" onload=""init()"">
</script>   

</head>
<body>
<div>
<div class=""url"">
    <span>URL={0}/[module]/[method]/[param1]/[param2]/...</span> 
</div>
<table class=""table"">
<thead>
<tr class=""table-info"">
    <th class=""urlPath"">URL Path</th>
    <th class=""returnType"">Return Type</th>
    <th class=""methodParams"">Method and Params</th> 
</tr>
<thead>
<tbody>
{2}
</tbody>
</table> </div> </body></html>
";



        public static readonly string RpcStyleTemplate = @"
<style type=""text/css"">
body {
    font-family: -apple-system,system-ui,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;
    font-size: 1rem;
    font-weight: 400;
    line-height: 1.5;
    color: #292b2c;
    background-color: #fff;
    margin: 0px;
    padding: 0px;
}
table {  background-color: transparent;  display: table; border-collapse: separate;  border-color: grey; }
.table { width: 100%; max-width: 100%;  margin-bottom: 1rem; }
.table th {  height: 30px; }
.table td, .table th {    border-bottom: 1px solid #eceeef;   text-align: left; padding-left: 16px;}
th.urlPath {  width: 10%; }
th.returnType {  width: 10%; }
th.methodParams {   width: 80%; } 
td.returnType { text-align: right; }
thead { display: table-header-group; vertical-align: middle; border-color: inherit;}
tbody { display: table-row-group; vertical-align: middle; border-color: inherit;}
tr { display: table-row;  vertical-align: inherit; border-color: inherit; }
.table-info, .table-info>td, .table-info>th { background-color: #dff0d8; }
.url { margin: 4px 0; padding-left: 16px;}
</style>
";

        public static readonly string RpcMethodTemplate = @"
<tr>
    <td class=""urlPath""> <a href=""{0}"">{0}</a>  </td>
    <td class=""returnType"">{1}</td>
    <td class=""methodParams"">
        <code><strong><a href=""{0}"" target=""_blank"">{2}</a></strong>({3})</code>
    </td> 
</tr>
";
    } 
}