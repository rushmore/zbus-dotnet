using CommandLine;
using CommandLine.Text;
using log4net;
using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{ 
    class ConsumerSimple
    { 

        static void Main(string[] args)
        { 
            Broker broker = new Broker("localhost:15555");
            Consumer c = new Consumer(broker, "MyTopic");  
            c.MessageHandler += (msg, client) => {
                Console.WriteLine(msg);
            };
            c.Start();

            Console.WriteLine("Consumer on MyTopic started");
            Console.ReadKey();
        }
    }
}
