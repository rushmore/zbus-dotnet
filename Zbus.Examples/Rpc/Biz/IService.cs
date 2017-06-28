using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Api.Example
{
    /// <summary>
    /// lower case methods only for test to language like java/javascript
    /// </summary>
    public interface IService
    {
        string echo(string msg);

        string testEncoding();  

        void noReturn();

        int plus(int a, int b); 

        void throwException();


        Task<string> GetStringAsync();

        Task<int> PlusAsync(int a, int b);
    }
}
