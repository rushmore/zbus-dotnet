using log4net;
using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class ConsumerExample
    { 
        static void Main(string[] args)
        { 
            Broker broker = new Broker();
            broker.AddTracker("localhost:15555");

            Consumer c = new Consumer(broker, "MyTopic");
            c.MessageHandler += (msg, client) => {
                Console.WriteLine(msg);
            };
            c.Start();

            Console.WriteLine("done");
            Console.ReadKey();
        }
    }
}
