using NUnit.Framework;
using Ploeh.AutoFixture;
using FluentAssertions; 
using Zbus.Mq.Net;


namespace Zbus.Mq.Net.Test
{
    public class ByteBuffer_Tests
    {
        private IFixture fixture;
        [SetUp]
        public void TestSetup()
        {
            fixture = new Fixture();
        }


        [Test]
        public void Test_IoBuffer_Size_From0([Range(0, 0)]int size)
        {
            //Arrange   
            ByteBuffer buf = new ByteBuffer(size);

            //Act 

            //Assert
            buf.Capacity.Should().Be(size);
        }

        [Test]
        public void Test_IoBuffer_Size([Random(0, 1024 * 1024 * 10, 10)]int size)
        {
            //Arrange   
            ByteBuffer buf = new ByteBuffer(size);

            //Act 

            //Assert
            buf.Capacity.Should().Be(size);
        }


        [Test]
        public void Test_Duplicate([Random(0, 1024 * 1024 * 10, 10)]int size)
        {
            //Arrange   
            ByteBuffer buf = new ByteBuffer(size);
            buf.Put(new byte[size / 2]);

            //Act 
            ByteBuffer buf2 = buf.Duplicate();

            //Assert
            buf2.ShouldBeEquivalentTo(buf);
        }
    }
}
