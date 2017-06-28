using System;
using Zbus.Mq.Net;
using Zbus.Mq;
using System.Threading.Tasks;
using System.Threading;

namespace Zbus.Examples
{
    class MqClientExample
    { 

        static void Main(string[] args)
        {
            Test().Wait();

            Console.WriteLine("done");
            Console.ReadKey();
        }

        static async Task Test()
        {
            MqClient client = new MqClient("localhost:15555"); 
            await client.ConnectAsync();  

            await client.DeclareGroupAsync("hong7", "hong77");
        }
    }
}
