
using System;
using System.IO;
using System.Text;

namespace Zbus.Mq.Net
{
    /// <summary>
    /// ICodec serialize or deserialize between domain object and binary data
    /// </summary>
    public interface ICodec
    {
        /// <summary>
        /// Encode the msg object to binary data 
        /// </summary>
        /// <param name="msg">domain object to serialize on wire</param>
        /// <returns>IoBuffer ready to read, encoder should be responsible to flip IoBuffer</returns>
        ByteBuffer Encode(object msg);

        /// <summary>
        /// Decode the buffer to domain object 
        /// </summary>
        /// <param name="buf">ByteBuffer read from</param>
        /// <returns>Decoded object or null if no ready</returns>
        object Decode(ByteBuffer buf);
    }


    /// <summary>
    /// ByteBuffer to manipulate auto-expansible binary array data.
    /// functionality similiar to Java's ByteBuffer
    /// </summary>
    public class ByteBuffer
    {
        /// <summary>
        /// Current read/write position of buffer
        /// </summary>
        public int Position { get; private set; }
        /// <summary>
        /// Max value allowded for <see cref="Position"/> to move.
        /// </summary>
        public int Limit { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public int Capacity { get; private set; }
        /// <summary>
        /// Internal data array
        /// </summary>
        public byte[] Data { get; private set; }

        private int mark;

        /// <summary>
        /// Create default IoBuffer with size 256
        /// </summary>
        public ByteBuffer() : this(256) { }
        /// <summary>
        /// Create IoBuffer from byte array, No copy.
        /// </summary>
        /// <param name="data">byte array consumed to be part of the IoBuffer</param>
        public ByteBuffer(byte[] data)
        {
            Data = data;
            Limit = Capacity = data.Length;
            mark = -1;
            Position = 0;
        }
        /// <summary>
        /// Create empty IoBuffer with specified capacity.
        /// </summary>
        /// <param name="capacity">IoBuffer capacity</param>
        public ByteBuffer(int capacity) : this(new byte[capacity])
        {

        }

        /// <summary>
        /// Duplicate the IoBuffer, No copy of the underlying data array.
        /// </summary>
        /// <returns>IoBuffer duplicated, all indice are reset</returns>
        public ByteBuffer Duplicate()
        {
            ByteBuffer buf = new ByteBuffer(Data);
            buf.Capacity = Capacity;
            buf.Limit = Limit;
            buf.mark = -1;
            buf.Position = Position;

            return buf;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="leftwardCount"></param>
        /// <returns></returns>
        public int Move(int leftwardCount)
        {
            if (leftwardCount > Position) return 0;
            Buffer.BlockCopy(Data, leftwardCount, Data, 0, Position - leftwardCount);
            Position -= leftwardCount;
            if (mark > Position) mark = -1;
            return leftwardCount;
        }
        /// <summary>
        /// Mark current position
        /// </summary>
        public void Mark()
        {
            mark = Position;
        }
        /// <summary>
        /// Reset the postion to last mark
        /// </summary>
        public void Reset()
        {
            int m = mark;
            if (m < 0)
            {
                throw new System.SystemException("reset state invalid");
            }
            Position = mark;
        }
        /// <summary>
        /// Calculate the remaining data length in bytes.
        /// </summary>
        /// <returns></returns>
        public int Remaining()
        {
            return Limit - Position;
        }

        /// <summary>
        /// Flip the IoBuffer, Limit become the Position last write.
        /// Position set to 0, to readable status. Can only be fliped once!
        /// </summary>
        public void Flip()
        {
            Limit = Position;
            Position = 0;
            mark = -1;
        }

        /// <summary>
        /// Set new limit value
        /// </summary>
        /// <param name="newLimit">new Limit value to set</param>
        public void SetNewLimit(int newLimit)
        {
            if (newLimit > Capacity || newLimit < 0)
            {
                throw new System.SystemException("new limit invalid");
            }
            Limit = newLimit;
            if (Position > Limit) Position = Limit;
            if (mark > Limit) mark = -1;
        }

        private void AutoExpand(int need)
        {
            int newCap = Capacity;
            int newSize = Position + need;
            while (newSize > newCap)
            {
                newCap *= 2;
            }
            if (newCap == Capacity) return;//nothing changed

            byte[] newData = new byte[newCap];
            Buffer.BlockCopy(Data, 0, newData, 0, Data.Length);
            Data = newData;
            Capacity = newCap;
            Limit = newCap;
        }

        /// <summary>
        /// Bypass n bytes for current IoBuffer
        /// </summary>
        /// <param name="n">count of bytes to bypass</param>
        public void Drain(int n)
        {
            int newPos = Position + n;
            if (newPos > Limit)
            {
                newPos = Limit;
            }
            Position = newPos;
            if (mark > Position) mark = -1;
        }
        /// <summary>
        /// Append string with format to the buffer
        /// </summary>
        /// <param name="format">format of string</param>
        /// <param name="args">args for the format string</param>
        public void Put(string format, params object[] args)
        {
            Put(string.Format(format, args));
        }
        /// <summary>
        /// Append an offset-based binary data to the buffer
        /// </summary>
        /// <param name="data">binary data to write</param>
        /// <param name="offset">offset of the data to write from</param>
        /// <param name="count">count of the data from offset to write</param>
        public void Put(byte[] data, int offset, int count)
        {
            AutoExpand(count);
            Buffer.BlockCopy(data, offset, Data, Position, count);
            Drain(count);
        }

        /// <summary>
        /// Append a whole binary to the buffer
        /// </summary>
        /// <param name="data">binary data</param>
        public void Put(byte[] data)
        {
            Put(data, 0, data.Length);
        }

        /// <summary>
        /// Append a string with the encoding to get underyling bytes
        /// </summary>
        /// <param name="data">string data to write</param>
        /// <param name="encoding">encoding used to get bytes</param>
        public void Put(string data, Encoding encoding)
        {
            Put(encoding.GetBytes(data));
        }
        /// <summary>
        /// Append a string with default encoding
        /// </summary>
        /// <param name="data">string data</param>
        public void Put(string data)
        {
            Put(data, Encoding.Default);
        }
        /// <summary>
        /// Append a key-value pair to the buffer, key-value format are HTTP
        /// compatible, ie, seperated with ': ' and ended with '\r\n'
        /// </summary>
        /// <param name="key">key string</param>
        /// <param name="value">value object</param>
        public void PutKeyValue(string key, object value)
        {
            Put(key);
            Put(": ");
            Put(value.ToString());
            Put("\r\n");
        }
        /// <summary>
        /// Copy out data from current position, Position not changed after copy.
        /// </summary>
        /// <param name="copy">data copied to</param>
        /// <returns>-1 if not enough to copy, total length copied otherwise</returns>
        public int Copyout(byte[] copy)
        {
            if (Remaining() < copy.Length)
            {
                return -1;
            }
            Buffer.BlockCopy(Data, Position, copy, 0, copy.Length);
            return copy.Length;
        }

        /// <summary>
        /// Read a specified length of data from the buffer. Position changed after sucessful get.
        /// </summary>
        /// <param name="len">length of data to copy</param>
        /// <returns>excatly specified length of bytes of data, null otherwise</returns>
        public byte[] Get(int len)
        {
            byte[] copy = new byte[len];
            int res = Copyout(copy);
            if (res != copy.Length)
            {
                return null;
            }
            Drain(len);
            return copy;
        }
        /// <summary>
        /// Use default encoding to change the underlying data from bytes to string.
        /// </summary>
        /// <returns>encoded string</returns>
        public override string ToString()
        {
            return Encoding.Default.GetString(Data, 0, Position);
        }

        /// <summary>
        /// Use specified encoding to change the underlying data from bytes to string.
        /// </summary>
        /// <param name="encoding">encoding to read bytes</param>
        /// <returns>encoded string</returns>
        public string ToString(Encoding encoding)
        {
            return encoding.GetString(Data, 0, Position);
        }

        /// <summary>
        /// Write the data from 0 to Position to stream
        /// </summary>
        /// <param name="stream">Stream object</param>
        public void WriteTo(Stream stream)
        {
            stream.Write(Data, 0, Position);
        }
    }
}
