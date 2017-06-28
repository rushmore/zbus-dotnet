using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Zbus.Mq.Net
{
    public class MessageCodec : ICodec
    {  
        public ByteBuffer Encode(object obj)
        {
            if (!(obj is Message))
            {
                throw new ArgumentException("Message type required for: " + obj);
            }
            Message msg = obj as Message;
            ByteBuffer buf = new ByteBuffer();
            Encode(buf, msg);

            buf.Flip();

            return buf;
        }

        public object Decode(ByteBuffer buf)
        {
            int idx = FindHeaderEnd(buf);
            int headLen = idx - buf.Position + 1;
            if (idx < 0) return null;

            string header = System.Text.Encoding.Default.GetString(buf.Data, buf.Position, headLen);

            Message msg = DecodeHeader(header);
            string bodyLenString = msg["content-length"];
            if (bodyLenString == null)
            {
                buf.Drain(headLen);
                return msg;
            }
            int bodyLen = int.Parse(bodyLenString);
            if (buf.Remaining() < headLen + bodyLen)
            {
                return null;
            }
            buf.Drain(headLen);
            byte[] body = buf.Get(bodyLen);
            msg.SetBody(body);
            return msg;
        }

        public void Encode(ByteBuffer buf, Message msg)
        {
            if (msg.Status != null)
            {
                string desc = "Unknow status";
                if (HttpStatusTable.ContainsKey(msg.Status.Value))
                {
                    desc = HttpStatusTable[msg.Status.Value];
                }
                buf.Put("HTTP/1.1 {0} {1}\r\n", msg.Status.Value, desc);
            }
            else
            {
                string method = msg.Method;
                if (method == null)
                {
                    method = "GET";
                }
                string url = msg.Url;
                if (url == null)
                {
                    url = "/";
                }
                buf.Put("{0} {1} HTTP/1.1\r\n", method, url);
            }
            foreach (KeyValuePair<string, string> e in msg.Headers)
            {
                buf.Put("{0}: {1}\r\n", e.Key, e.Value);
            }
            string lenKey = "content-length";
            if (!msg.Headers.ContainsKey(lenKey))
            {
                int bodyLen = msg.Body == null ? 0 : msg.Body.Length;
                buf.Put("{0}: {1}\r\n", lenKey, bodyLen);
            }

            buf.Put("\r\n");

            if (msg.Body != null)
            {
                buf.Put(msg.Body);
            }
        }


        private static int FindHeaderEnd(ByteBuffer buf)
        {
            int i = buf.Position;
            byte[] data = buf.Data;
            while (i + 3 < buf.Limit)
            {
                if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                {
                    return i + 3;
                }
                i++;
            }
            return -1;

        }

        private static Message DecodeHeader(string header)
        {
            Message msg = new Message();
            string[] lines = Regex.Split(header, "\r\n");
            string meta = lines[0].Trim();
            string[] blocks = meta.Split(' ');
            string test = blocks[0].ToUpper();
            if (test.StartsWith("HTTP"))
            {
                msg.Status = int.Parse(blocks[1]);
            }
            else
            {
                msg.Method = blocks[0];
                if (blocks.Length > 1)
                {
                    msg.Url = blocks[1];
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int idx = line.IndexOf(':');
                if (idx < 0) continue; //ignore
                string key = line.Substring(0, idx).Trim().ToLower(); //key to lower case
                string val = line.Substring(idx + 1).Trim();
                msg.SetHeader(key, val);
            }

            return msg;
        } 

        private static readonly IDictionary<int, string> HttpStatusTable = new Dictionary<int, string>();
        static MessageCodec()
        {
            HttpStatusTable.Add(200, "OK");
            HttpStatusTable.Add(201, "Created");
            HttpStatusTable.Add(202, "Accepted");
            HttpStatusTable.Add(204, "No Content");
            HttpStatusTable.Add(206, "Partial Content");
            HttpStatusTable.Add(301, "Moved Permanently");
            HttpStatusTable.Add(304, "Not Modified");
            HttpStatusTable.Add(400, "Bad Request");
            HttpStatusTable.Add(401, "Unauthorized");
            HttpStatusTable.Add(403, "Forbidden");
            HttpStatusTable.Add(404, "Not Found");
            HttpStatusTable.Add(405, "Method Not Allowed");
            HttpStatusTable.Add(416, "Requested Range Not Satisfiable");
            HttpStatusTable.Add(500, "Internal Server Error");
        }

    }

    public class MessageClient : Client<Message>
    {
        public MessageClient(string serverAddress)
            : base(serverAddress, new MessageCodec())
        {
        }
        public MessageClient(ServerAddress serverAddress, string certFile = null)
            : base(serverAddress, new MessageCodec(), certFile)
        {
        }
    } 
}
