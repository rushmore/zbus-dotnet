using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zbus.Examples
{
    public interface IService
    {
        string echo(string msg);

        string testEncoding();

        void noReturn();

        int plus(int a, int b);

        void throwException();


        Task<string> getString(string req); //GetStringAsync => getString for inter-language purpose only

    }

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

        public Task<string> getString(string req)
        {
            return Task.Run(() =>
            {
                return "AsyncTask: " + req;
            });
        } 
    }
}
