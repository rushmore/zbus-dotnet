using System;
using Zbus.Mq.Net;
using Zbus.Mq;
using System.Threading.Tasks;
using System.Threading;

namespace Zbus.Examples
{
    class MqClientStartExample
    { 

        static void Main(string[] args)
        {
            Test().Wait(); 

            Console.ReadKey();
        }

        static async Task Test()
        {
            MqClient client = new MqClient("localhost:15555");
            client.MessageReceived += (msg) =>
            {
                Console.WriteLine(JsonKit.SerializeObject(msg)); 
            };
            client.Connected += async() =>
            {
                Console.WriteLine("connected");
                Message req = new Message
                {
                    Cmd = Protocol.TRACK_SUB,
                };
                await client.SendAsync(req);
            }; 
            client.Disconnected += () =>
            {
                Console.WriteLine("disconnected");
            }; 
            client.Start();  
        }
    }
}
