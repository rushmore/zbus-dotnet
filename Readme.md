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

Start zbus, please refer to [https://gitee.com/rushmore/zbus](https://gitee.com/rushmore/zbus) 

zbus's .NET client provides friendly and very easy API for .NET platform.

## Getting started

zbus has a very few dependencies, simply install

    PM> Install-Package Zbus

## API Demo

Only demos the gist of API, more configurable usage calls for your further interest.

### Produce message

    Broker broker = new Broker("localhost:15555");  //Load balance for all tracked servers.
    Producer p = new Producer(broker);
    Message msg = new Message
    {
        Topic = "hong",
        BodyString = "From Zbus.NET",
    };
    await p.ProduceAsync(msg);



### Consume message

    Broker broker = new Broker("localhost:15555"); 
    Consumer c = new Consumer(broker, "MyTopic");
    c.MessageHandler += (msg, client) => {
        Console.WriteLine(msg);
    };
    c.Start();

### RPC client

    using (Broker broker = new Broker("localhost:15555"))
    {
        RpcInvoker rpc = new RpcInvoker(broker, "MyRpc"); 
        //Way 1) Raw invocation
        var res = rpc.InvokeAsync<int>("plus", 1, 2).Result;
        Console.WriteLine(res);

        //Way 2) Dynamic Object
        dynamic rpc2 = rpc;                          //RpcInvoker is also a dynamic object
        var res2 = rpc2.plus(1, 2);                  //Magic!!! just like javascript
        Console.WriteLine(res2);

        //Way 3) Strong typed class proxy
        IService svc = rpc.CreateProxy<IService>();  //Create a proxy class, strongly invocation

        var res3 = svc.plus(1, 2);
        Console.WriteLine(res3);
    }

### RPC service

    RpcProcessor p = new RpcProcessor();
    p.AddModule<MyService>(); //Simple? No requirements on your business object(MyService)!

    Broker broker = new Broker("localhost:15555"); //Capable of HA failover, test it!  
    Consumer c = new Consumer(broker, "MyRpc");
    c.ConnectionCount = 4; 
    c.MessageHandler = p.MessageHandler; //Set processor as message handler
    c.Start();