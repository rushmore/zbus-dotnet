using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class MqAdminExample
    {
        static async Task Test()
        {
            Broker broker = new Broker();
            broker.AddTracker("localhost:15555");

            MqAdmin admin = new MqAdmin(broker);
            await admin.DeclareTopicAsync("hong8");

            ServerInfo[] infoArray = await admin.QueryServerAsync();
            foreach(ServerInfo info in infoArray)
            {
                Console.WriteLine(info.ServerAddress);
            }

            Console.WriteLine("done");
        }
        static void Main(string[] args)
        {
            Test().Wait(); 
            Console.ReadKey();
        }
    }
}
