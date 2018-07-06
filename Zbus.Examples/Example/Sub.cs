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
    class SubExample
    { 
        static void Main(string[] args)
        { 
            MqClient client = new MqClient("localhost:15555");

            const string mq = "MyMQ";
            const string channel = "MyChannel";
            client.OnOpen += async (c) =>
            {
                Message data = new Message();
                data.Headers["cmd"] = "create";
                data.Headers["mq"] =  mq;
                data.Headers["channel"] = channel;
                 
                var res = await client.InvokeAsync(data);
                Console.WriteLine(JsonKit.SerializeObject(res));

                data = new Message();
                data.Headers["cmd"] = "sub";
                data.Headers["mq"] = mq;
                data.Headers["channel"] = channel;
                data.Headers["window"] = 1;

                res = await client.InvokeAsync(data);
                Console.WriteLine(JsonKit.SerializeObject(res));
            };

            client.AddMqHandler(mq, channel, (msg) =>
            {
                Console.WriteLine(JsonKit.SerializeObject(msg));
            });

            client.ConnectAsync().Wait();
        } 
    }
}
