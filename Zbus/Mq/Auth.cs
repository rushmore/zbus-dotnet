using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace zbus
{

    public class Auth
    {
        static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        static void Sort(JObject jObj)
        { 
            var props = jObj.Properties().ToList();
            foreach (var prop in props)
            {
                prop.Remove();
            }

            foreach (var prop in props.OrderBy(p => p.Name))
            {
                jObj.Add(prop);
                if (prop.Value is JObject)
                    Sort((JObject)prop.Value);
                if (prop.Value is JArray)
                {
                    Int32 iCount = prop.Value.Count();
                    for (Int32 iIterator = 0; iIterator < iCount; iIterator++)
                        if (prop.Value[iIterator] is JObject)
                            Sort((JObject)prop.Value[iIterator]);
                }
            }
        }
        
        public static void Sign(string apiKey, string secretKey, Message msg)
        {
            msg.Headers["apiKey"] = apiKey;
            msg.Headers.Remove("signature");

            var str = JsonConvert.SerializeObject(msg, jsonSettings);
            var json = (JObject)JsonConvert.DeserializeObject(str);
            Sort(json);
            str = JsonConvert.SerializeObject(json, jsonSettings);

            Encoding encoding = Encoding.UTF8;
            using (HMACSHA256 hmac = new HMACSHA256(encoding.GetBytes(secretKey)))
            {
                var bytes = hmac.ComputeHash(encoding.GetBytes(str));
                var sign = BitConverter.ToString(bytes).Replace("-", "").ToLower();
                msg.Headers["signature"] = sign;
            }
        } 
    }
}
