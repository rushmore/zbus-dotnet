using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using zbus;
#pragma warning disable CS4014

namespace zbus.Examples
{
    class WebsocketExample
    { 
        static void Websocket(string[] args)
        { 
            WebsocketClient ws = new WebsocketClient("localhost:15555"); 

            ws.BeforeSend = (msg) =>
            {
                msg.Headers["cmd"] = "pub";
                msg.Headers["mq"] = "MyRpc";
                msg.Headers["ack"] = "false";
            };

            ws.OnOpen += async (client) =>
            {
                IDictionary<string, object> data = new Dictionary<string, object>();
                data["method"] = "plus";
                data["params"] = new object[] { 1, 2 };
                data["module"] = "/"; 
        

                for (int i = 0; i < 100; i++)
                {
                    Message msg = new Message
                    {
                        Body = data
                    };

                    var res = await ws.InvokeAsync(msg);
                    Console.WriteLine(JsonKit.SerializeObject(res));
                }
            };

            ws.ConnectAsync();
        } 
    }
}
