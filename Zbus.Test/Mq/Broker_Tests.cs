using NUnit.Framework;
using Ploeh.AutoFixture;
using FluentAssertions;
namespace Zbus.Mq.Test
{
    public class Broker_Tests
    {
        private IFixture fixture;
        [SetUp]
        public void TestSetup()
        {
            fixture = new Fixture();
        }


        [Test]
        public void Test_Broker()
        {
            //Arrange    
            Broker broker = new Broker("localhost:15555");

            //Act 
            broker.Dispose();
            //Assert 
            broker.PoolTable.Should().BeEmpty();
        }

    }
}
