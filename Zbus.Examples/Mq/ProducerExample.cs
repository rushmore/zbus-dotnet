using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class ProducerExample
    {
        static async Task Test()
        {
            Broker broker = new Broker();
            broker.AddTracker("localhost:15555");

            Producer p = new Producer(broker);
            Message msg = new Message
            {
                Topic = "hong",
                BodyString = "From Zbus.NET",
            };
            await p.ProduceAsync(msg);

            Console.WriteLine("done");
        }

        static void Main(string[] args)
        {
            Test().Wait();
            Console.ReadKey();
        }
    }
}
