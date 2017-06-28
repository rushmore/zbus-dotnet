using System;
using System.Threading;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class ConsumeThreadExample
    { 
        static void Main(string[] args)
        { 
            ConsumeThread thread = new ConsumeThread("hong")
            { 
                ClientFactory = () =>
                {
                    return new MqClient("localhost:15555");
                },
                ConnectionCount = 1,
            };

            thread.MessageRecevied += (msg, client) =>
            {
                Console.WriteLine(msg);
            };

            thread.Start(); 


            Console.WriteLine("done");
            Console.ReadKey();
        }
    }
}
