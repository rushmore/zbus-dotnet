using Api.Example;
using System;
using System.Threading.Tasks;
using Zbus.Mq;
using Zbus.Rpc;

namespace Zbus.Examples
{
    class RpcInvokerExample
    {
        static async Task Test(IService svc)
        {
            int res = await svc.PlusAsync(1, 2);  //support Async keywords
            Console.WriteLine(res);
        }

        static void Main(string[] args)
        { 
            using (Broker broker = new Broker("localhost:15555"))
            {
                RpcInvoker rpc = new RpcInvoker(broker, "MyRpc"); 
                //Way 1) Raw invocation
                var res = rpc.InvokeAsync<int>("plus", 1, 2).Result;
                Console.WriteLine(res);

                //Way 2) Dynamic Object
                dynamic rpc2 = rpc;                          //RpcInvoker is also a dynamic object
                var res2 = rpc2.plus(1, 2);                  //Magic!!! just like javascript
                Console.WriteLine(res2);

                //Way 3) Strong typed class proxy
                IService svc = rpc.CreateProxy<IService>();  //Create a proxy class, strongly invocation

                var res3 = svc.plus(1, 2);
                Console.WriteLine(res3);
            }

            Console.WriteLine("done");
            Console.ReadKey();
        }
    }
}
