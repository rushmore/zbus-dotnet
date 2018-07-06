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
    class RpcClientExample
    {  
        static async Task Test()
        {
            using (RpcClient rpc = new RpcClient("localhost:15555"))
            {
                //rpc.AuthEnabled = true;
                //rpc.ApiKey = "2ba912a8-4a8d-49d2-1a22-198fd285cb06";
                //rpc.SecretKey = "461277322-943d-4b2f-b9b6-3f860d746ffd";
                 
                //dynamic
                IService svc = rpc.CreateProxy<IService>("/example");
                int c = svc.plus(1, 2);
                Console.WriteLine(c); 

                int res = await rpc.InvokeAsync<int>("/example/plus", new object[] { 1, 2 });
                Console.WriteLine(JsonKit.SerializeObject(res));

                //string str = await svc.getString("hong"); //support remote await!
                //Console.WriteLine(str); 
            }
        }
        static void Main(string[] args)
        {
            try
            {
                Test().Wait();
            }
            catch(Exception e)
            {
                while(e.InnerException != null)
                {
                    e = e.InnerException;
                }
                Console.WriteLine(e);
            }
            
            Console.ReadKey();
        } 
    }
}
