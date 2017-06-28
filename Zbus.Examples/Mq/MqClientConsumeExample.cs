using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class MqClientConsumeExample
    {
        static async Task Test()
        {
            MqClient client = new MqClient("localhost:15555"); 

            Message msg = await client.ConsumeAsync("hong5");
            Console.WriteLine(msg.Headers); 
        }
        static void Main(string[] args)
        {
            Test().Wait();
            Console.ReadKey();
        }
    }
}
