using Api.Example;
using System;
using System.Threading;
using Zbus.Mq;
using Zbus.Rpc;

namespace Zbus.Examples
{
    class RpcProcessorExample
    { 
        static void Main(string[] args)
        { 
            RpcProcessor p = new RpcProcessor();
            p.AddModule<MyService>(); //Simple?


            Broker broker = new Broker("localhost:15555"); //Capable of HA failover, test it! 

            Consumer c = new Consumer(broker, "MyRpc");
            c.ConnectionCount = 2; 
            c.MessageHandler = p.MessageHandler; //Set processor as message handler
            c.Start();
            Console.WriteLine("Rpc Service Ready");
        }
    }
}
