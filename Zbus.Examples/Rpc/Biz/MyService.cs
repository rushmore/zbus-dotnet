using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks; 
namespace Api.Example
{
    public class MyService : IService
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

        public Task<string> GetStringAsync()
        {
            return Task.Run(() =>
            {
                Thread.Sleep(100);
                return "Sleep(100)";
            });
        }

        public Task<int> PlusAsync(int a, int b)
        {
            return Task.Run(() =>
            {
                return a + b;
            });
        } 
    }
}
