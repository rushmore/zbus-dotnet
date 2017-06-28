using NUnit.Framework;
using Ploeh.AutoFixture;
using FluentAssertions;
namespace Zbus.Mq.Test
{
    public class MqClient_Tests
    {
        private IFixture fixture;
        [SetUp]
        public void TestSetup()
        {
            fixture = new Fixture();
        }


        [Test]
        public void MqClientConnect_Close()
        {
            //Arrange    
            MqClient client = new MqClient("localhost:15555");
            client.ConnectAsync().Wait();

            //Act 
            client.Dispose();
            //Assert 
            client.Active.Should().BeFalse();
        }

    }
}
