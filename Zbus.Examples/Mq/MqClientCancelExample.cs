using System;
using Zbus.Mq.Net;
using Zbus.Mq;
using System.Threading.Tasks;
using System.Threading;

namespace Zbus.Examples
{
    class MqClientCancelExample
    {
        static async Task Test(MqClient client, CancellationToken token)
        {
            Message msg = new Message
            {
                Cmd = Protocol.QUERY,
                Topic = "hong7",
                ConsumeGroup = "hong7",
                Id = Guid.NewGuid().ToString(),
            };

            Console.WriteLine($"request: {msg.Id}");
            Message res = await client.InvokeAsync(msg, token);
            Console.WriteLine($"response: {msg}");
        }

        static void Main(string[] args)
        {
            MqClient client = new MqClient("zbus.io");
            client.ConnectAsync().Wait();


            for (int i = 0; i < 10; i++)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Test(client, cts.Token);
                
                Task.Run(() =>
                {
                    Thread.Sleep(new Random().Next(0,1));
                    cts.Cancel();
                });
                Console.WriteLine($"======={i}=======");
                Thread.Sleep(100);
            }
            
            Console.ReadKey();
        }
    }
}
