                /\\\                                                                                            
                \/\\\                                                                                           
                 \/\\\                                                                          /\\\            
     /\\\\\\\\\\\ \/\\\         /\\\    /\\\  /\\\\\\\\\\        /\\/\\\\\\       /\\\\\\\\   /\\\\\\\\\\\      
     \///////\\\/  \/\\\\\\\\\  \/\\\   \/\\\ \/\\\//////        \/\\\////\\\    /\\\/////\\\ \////\\\////      
           /\\\/    \/\\\////\\\ \/\\\   \/\\\ \/\\\\\\\\\\       \/\\\  \//\\\  /\\\\\\\\\\\     \/\\\         
          /\\\/      \/\\\  \/\\\ \/\\\   \/\\\ \////////\\\       \/\\\   \/\\\ \//\\///////      \/\\\ /\\    
         /\\\\\\\\\\\ \/\\\\\\\\\  \//\\\\\\\\\   /\\\\\\\\\\  /\\\ \/\\\   \/\\\  \//\\\\\\\\\\    \//\\\\\    
         \///////////  \/////////    \/////////   \//////////  \///  \///    \///    \//////////      \/////  
         
# zbus-dotnet

zbus strives to make Message Queue and Remote Procedure Call fast, light-weighted and easy to build your own service-oriented architecture for many different platforms. Simply put, zbus = mq + rpc.

zbus carefully designed on its protocol and components to embrace KISS(Keep It Simple and Stupid) principle, but in all it delivers power and elasticity. 

- Working as MQ, compare it to RabbitMQ, ActiveMQ.
- Working as RPC, compare it to many more.

Start zbus, please refer to [https://github.com/rushmore/zbus](https://github.com/rushmore/zbus) 

zbus's .NET client provides friendly and very easy API for .NET platform.

## Getting started

zbus has a very few dependencies, simply install

    PM> Install-Package Zbus

## API Demo

Only demos the gist of API, more configurable usage calls for your further interest.

### Produce message

    using (MqClient client = new MqClient("localhost:15555"))
    {   
        var data = new Message();
        data.Headers["cmd"] = "pub";
        data.Headers["mq"] = mq;
        data.Body = "Hello from C#";

        res = await client.InvokeAsync(data);
        Console.WriteLine(JsonKit.SerializeObject(res));
    } 



### Consume message

    MqClient client = new MqClient("localhost:15555");

    const string mq = "MyMQ";
    const string channel = "MyChannel";
    client.OnOpen += async (c) =>
    {
        //创建MQ
        Message data = new Message();
        data.Headers["cmd"] = "create";
        data.Headers["mq"] =  mq;
        data.Headers["channel"] = channel;
            
        var res = await client.InvokeAsync(data);
        Console.WriteLine(JsonKit.SerializeObject(res));

        //发送订阅命令
        data = new Message();
        data.Headers["cmd"] = "sub";
        data.Headers["mq"] = mq;
        data.Headers["channel"] = channel;

        res = await client.InvokeAsync(data);
        Console.WriteLine(JsonKit.SerializeObject(res));
    };

    client.AddMqHandler(mq, channel, (msg) =>
    {
        Console.WriteLine(JsonKit.SerializeObject(msg));
    });

    client.ConnectAsync().Wait();

### RPC client

    using (RpcClient rpc = new RpcClient("localhost:15555", "MyRpc"))
    { 
        string module = "";   
        //dynamic
        IService svc = rpc.CreateProxy<IService>(module);
        int c = svc.plus(1, 2);
        Console.WriteLine(c);

        string str = await svc.getString("hong"); //support remote await!
        Console.WriteLine(str);

        //Raw API
        int res = await rpc.InvokeAsync<int>("plus", new object[] { 1, 2 }, module: module);
        Console.WriteLine(JsonKit.SerializeObject(res)); 
    } 

### RPC service

    class RpcServerExample
    {
        
        public string echo(string msg)
        {
            return msg;
        }

        public string testEncoding()
        {
            return "中文";
        }

        public void noReturn()
        {

        }

        public int plus(int a, int b)
        {
            return a + b;
        }

        public void throwException()
        {
            throw new NotImplementedException();
        }

        [RequestMapping("/abc")]
        public Message home()
        {
            Message res = new Message();
            res.Status = 200;
            res.Headers["content-type"] = "text/html; charset=utf8";
            res.Body = "<h1>C# body</h1>";
            return res;
        }

        public Task<string> getString(string req)
        {
            return Task.Run(() =>
            {
                return "AsyncTask: " + req;
            });
        }

        static void Main(string[] args)
        {
            RpcProcessor p = new RpcProcessor();
            p.UrlPrefix = "";
            p.Mount("/example", new RpcServerExample());


            //RPC via MQ
            RpcServer server = new RpcServer(p);
            server.MqServerAddress = "localhost:15555";

            //server.AuthEnabled = true;
            //server.ApiKey = "2ba912a8-4a8d-49d2-1a22-198fd285cb06";
            //server.SecretKey = "461277322-943d-4b2f-b9b6-3f860d746ffd";

            server.Mq = "MyRpc"; 
            server.Start(); 
        } 
    }