using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using zbus;

namespace zbus.Examples
{ 
    class RpcServerExample
    {
        
        public string echo(string msg)
        {
            return msg;
        }

        public string testEncoding()
        {
            return "中文";
        }

        public void noReturn()
        {

        }

        public int plus(int a, int b)
        {
            return a + b;
        }

        public void throwException()
        {
            throw new NotImplementedException();
        }

        [RequestMapping("/")]
        public Message home()
        {
            Message res = new Message();
            res.Status = 200;
            res.Headers["content-type"] = "text/html; charset=utf8";
            res.Body = "<h1>C# body</h1>";
            return res;
        }

        public Task<string> getString(string req)
        {
            return Task.Run(() =>
            {
                return "AsyncTask: " + req;
            });
        }

        static void Main(string[] args)
        {
            RpcProcessor p = new RpcProcessor();
            p.UrlPrefix = "";
            p.Mount("/example", new RpcServerExample());


            //RPC via MQ
            RpcServer server = new RpcServer(p);
            server.MqServerAddress = "localhost:15555";

            //server.AuthEnabled = true;
            //server.ApiKey = "2ba912a8-4a8d-49d2-1a22-198fd285cb06";
            //server.SecretKey = "461277322-943d-4b2f-b9b6-3f860d746ffd";

            server.Mq = "MyRpc";
            
            server.Start(); 
        } 
    }
}
