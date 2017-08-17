using CommandLine;
using CommandLine.Text;
using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{  

    class ProducerSimple
    {
        static async Task Test()
        {  
            using (Broker broker = new Broker("localhost:15555")) 
            {
                Producer p = new Producer(broker);
                Message msg = new Message
                {
                    Topic = "MyTopic",
                    BodyString = "Hello, from .NET",
                }; 
                var res = await p.PublishAsync(msg);
                Console.WriteLine(res);
            } 
        }

        static void Main(string[] args)
        {  
            Test().Wait();
            Console.ReadKey();
        }
    }
}
