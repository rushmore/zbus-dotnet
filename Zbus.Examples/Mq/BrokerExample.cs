using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class BrokerExample
    { 
        static void Main(string[] args)
        {
            Broker broker = new Broker("localhost:15555");

            Console.WriteLine(broker.RouteTable.ServerTable.Count);
            Console.ReadKey();
        }
    }
}
