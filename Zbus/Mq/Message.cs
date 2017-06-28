using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Zbus.Mq.Net;

namespace Zbus.Mq
{
    public class Message : Id
    {
        public string Url { get; set; }
        public int? Status { get; set; }
        public string Method { get; set; } 
        public IDictionary<string, string> Headers { get; private set; }
        public byte[] Body { get; private set; }

        public Message()
        {
            Headers = new Dictionary<string, string>();
            Url = "/";
            Method = "GET";
            SetCommonHeaders();
        }
        private void SetCommonHeaders()
        {
            SetHeader("connection", "Keep-Alive");
            Version = Protocol.VERSION_VALUE;
        }

        public string this[string key]
        {
            get
            {
                if (Headers.ContainsKey(key))
                {
                    return Headers[key];
                }
                return null;
            }
            set
            {
                Headers[key] = value;
            }
        }

        public T? GetHeader<T>(string key, T? defaultValue=null) where T: struct
        { 
            string s = GetHeader(key);
            if (s == null) return defaultValue;
            return (T)Convert.ChangeType(s, typeof(T)); 
        }

        public string GetHeader(string key, string defaultValue = null)
        {
            string val = null;
            this.Headers.TryGetValue(key, out val);
            if (val == null)
            {
                val = defaultValue;
            }
            return val;
        }

        public void SetHeader(string key, object val)
        {
            if (val == null) return;
            this.Headers[key] = (string)Convert.ChangeType(val, typeof(string));
        }

        public void RemoveHeader(string key)
        {
            this.Headers.Remove(key);
        }
         
        private System.Text.Encoding GetEncoding(System.Text.Encoding encoding = null)
        {
            if (Encoding != null) //use header encoding first!
            {
                try
                {
                    encoding = System.Text.Encoding.GetEncoding(Encoding);
                }
                catch
                {
                    //ignore 
                }
            }
            if (encoding == null)
            {
                encoding = System.Text.Encoding.UTF8;
            }
            return encoding;
        }
        public string GetBody(System.Text.Encoding encoding = null)
        {
            if (Body == null) return null; 
            return GetEncoding(encoding).GetString(Body);
        }

        /// <summary>
        /// Header encoding > parameter encoding
        /// </summary>
        /// <param name="body"></param>
        /// <param name="encodingName"></param>
        public void SetBody(byte[] body, string encodingName = null)
        {
            this.Body = body;
            int bodyLen = 0;
            if (this.Body != null)
            {
                bodyLen = this.Body.Length;
            } 
            Encoding = encodingName;
            this.SetHeader("content-length", string.Format("{0}", bodyLen));
        }

        public void SetBody(string body, Encoding encoding = null)
        {
            encoding = GetEncoding(encoding);
            SetBody(encoding.GetBytes(body), encoding.WebName);
        }
         
        public void SetBody(string format, params object[] args)
        {
            SetBody(string.Format(format, args));
        }

        public void SetJsonBody(string body, Encoding encoding = null)
        {
            this.SetBody(body, encoding);
            this.SetHeader("content-type", "application/json");
        }

        #region AUX_GET_SET

        public string Topic
        {
            get { return GetHeader(Protocol.TOPIC); }
            set { SetHeader(Protocol.TOPIC, value); }
        }

        public string ConsumeGroup
        {
            get { return GetHeader(Protocol.CONSUME_GROUP); }
            set { SetHeader(Protocol.CONSUME_GROUP, value); }
        }
        public int? ConsumeWindow
        {
            get { return GetHeader<int>(Protocol.CONSUME_WINDOW); }
            set { SetHeader(Protocol.CONSUME_WINDOW, value); }
        }
        public string GroupStartCopy
        {
            get { return GetHeader(Protocol.GROUP_START_COPY); }
            set { SetHeader(Protocol.GROUP_START_COPY, value); }
        }
        public long? GroupStartOffset
        {
            get { return GetHeader<long>(Protocol.GROUP_START_OFFSET); }
            set { SetHeader(Protocol.GROUP_START_OFFSET, value); }
        }

        public string GroupStartMsgid
        {
            get { return GetHeader(Protocol.GROUP_START_MSGID); }
            set { SetHeader(Protocol.GROUP_START_MSGID, value); }
        }
        public long? GroupStartTime
        {
            get { return GetHeader<long>(Protocol.GROUP_START_TIME); }
            set { SetHeader(Protocol.GROUP_START_TIME, value); }
        }

        public string GroupFilter
        {
            get { return GetHeader(Protocol.GROUP_FILTER); }
            set { SetHeader(Protocol.GROUP_FILTER, value); }
        }

        public int? GroupMask
        {
            get { return GetHeader<int>(Protocol.GROUP_MASK); }
            set { SetHeader(Protocol.GROUP_MASK, value); }
        }

        public int? TopicMask
        {
            get { return GetHeader<int>(Protocol.TOPIC_MASK); }
            set { SetHeader(Protocol.TOPIC_MASK, value); }
        }

        public string Cmd
        {
            get { return GetHeader(Protocol.COMMAND); }
            set { SetHeader(Protocol.COMMAND, value); }
        }

        public string Id
        {
            get { return GetHeader(Protocol.ID); }
            set { SetHeader(Protocol.ID, value); }
        } 

        public string Token
        {
            get { return GetHeader(Protocol.TOKEN); }
            set { SetHeader(Protocol.TOKEN, value); }
        }

        public string Sender
        {
            get { return GetHeader(Protocol.SENDER); }
            set { SetHeader(Protocol.SENDER, value); }
        }

        public string Recver
        {
            get { return GetHeader(Protocol.RECVER); }
            set { SetHeader(Protocol.RECVER, value); }
        } 
        public string Encoding
        {
            get { return GetHeader(Protocol.ENCODING); }
            set { SetHeader(Protocol.ENCODING, value); }
        }

        public string OriginUrl
        {
            get { return GetHeader(Protocol.ORIGIN_URL); }
            set { SetHeader(Protocol.ORIGIN_URL, value); }
        }

        public int? OriginStatus
        {
            get { return GetHeader<int>(Protocol.ORIGIN_STATUS); }
            set { SetHeader(Protocol.ORIGIN_STATUS, value); }
        }

        public string OriginId
        {
            get { return GetHeader(Protocol.ORIGIN_ID); }
            set { SetHeader(Protocol.ORIGIN_ID, value); }
        }

        public bool Ack
        {
            get
            {
                string ack = GetHeader(Protocol.ACK);
                return (ack == null) || "1".Equals(ack); //default to true 
            }
            set { SetHeader(Protocol.ACK, value ? "1" : "0"); }
        }

        public string Version
        {
            get { return GetHeader(Protocol.VERSION); }
            set { SetHeader(Protocol.VERSION, value); }
        } 

        public string BodyString
        {
            get { return GetBody(); }
            set { SetBody(value); }
        }

        #endregion

        public override string ToString()
        {
            ByteBuffer buf = new MessageCodec().Encode(this);
            System.Text.Encoding encoding = System.Text.Encoding.UTF8;
            if(Encoding != null)
            {
                try
                { 
                    encoding = System.Text.Encoding.GetEncoding(Encoding);
                }
                catch
                {
                    //ignore
                }
            }
            return encoding.GetString(buf.Data, 0, buf.Limit);
        }
    } 
}
