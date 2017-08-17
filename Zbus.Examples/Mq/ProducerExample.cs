using CommandLine;
using CommandLine.Text;
using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class ProduceOptions
    {
        [Option('t', "topic", Required = false, DefaultValue = "MyTopic", HelpText = "Topic to consume")]
        public string Topic { get; set; }
        [Option('m', "message", Required = false, DefaultValue = "Hello World From .NET", HelpText = "Message body")]
        public string Message { get; set; }
        [Option('b', "broker", Required = false, DefaultValue = "localhost:15555", HelpText = "Broker server(s), separated by ';'")]
        public string Broker { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }


    class ProducerExample
    {
        static async Task Test(ProduceOptions options)
        {  
            using (Broker broker = new Broker(options.Broker)) 
            {
                Producer p = new Producer(broker);
                Message msg = new Message
                {
                    Topic = options.Topic,
                    BodyString = options.Message,
                }; 
                var res = await p.PublishAsync(msg);
                Console.WriteLine(res);
            } 
        }

        static void Main(string[] args)
        {
            var options = new ProduceOptions();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                return;
            } 
            Test(options).Wait();
            Console.ReadKey();
        }
    }
}
