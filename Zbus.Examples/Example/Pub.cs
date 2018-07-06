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
    class PubExample
    { 
        static async Task Test()
        {
            using (MqClient client = new MqClient("localhost:15555"))
            {
                //1) Create MQ if necessary(empty in zbus), you may ommit this step
                const string mq = "MyMQ";
                Message data = new Message();
                data.Headers["cmd"] = "create";
                data.Headers["mq"] = mq;

                var res = await client.InvokeAsync(data);
                Console.WriteLine(JsonKit.SerializeObject(res));

                //2) Publish Message
                data = new Message();
                data.Headers["cmd"] = "pub";
                data.Headers["mq"] = mq;
                data.Body = "Hello from C#";

                res = await client.InvokeAsync(data);
                Console.WriteLine(JsonKit.SerializeObject(res));
            } 
        }
        static void Main(string[] args)
        {
            Test().Wait();
            Console.ReadKey();
        } 
    }
}
