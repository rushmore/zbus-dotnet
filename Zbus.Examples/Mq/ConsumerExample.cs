using CommandLine;
using CommandLine.Text;
using log4net;
using System;
using System.Threading.Tasks;
using Zbus.Mq;

namespace Zbus.Examples
{
    class ConsumeOptions
    {
        [Option('t', "topic", Required = false, DefaultValue ="MyTopic", HelpText = "Topic to consume")]
        public string Topic { get; set; }
        [Option('g', "group", Required = false, HelpText = "ConsumeGroup where Consumer in")]
        public string ConsumeGroup { get; set; }
        [Option('f', "filter", Required = false, HelpText = "Message filter for the ConsumeGroup")]
        public string MsgFilter { get; set; }
        [Option('c', "connCount", Required = false, DefaultValue = 1, HelpText = "Physical connection count for the Consumer")]
        public int ConnectionCount { get; set; }
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

    class ConsumerExample
    { 

        static void Main(string[] args)
        {
            var options = new ConsumeOptions();
            if( !CommandLine.Parser.Default.ParseArguments(args, options))
            {
                return;
            }
            ConsumeGroup group = new ConsumeGroup
            {
                GroupName = options.ConsumeGroup,
                Filter = options.MsgFilter,
            };
            if (options.ConsumeGroup == null)
            {
                group.GroupName = options.Topic;
            }


            Broker broker = new Broker(options.Broker);
            Consumer c = new Consumer(broker, options.Topic); 
            c.ConnectionCount = options.ConnectionCount;
            c.ConsumeGroup = group; 
            c.MessageHandler += (msg, client) => {
                Console.WriteLine(msg);
            };
            c.Start();

            Console.WriteLine("Consumer on Topic={0}, ConsumeGroup={1} started", options.Topic, group.GroupName);
            Console.ReadKey();
        }
    }
}
