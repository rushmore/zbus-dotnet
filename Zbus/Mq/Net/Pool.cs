using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Zbus.Mq.Net
{
     
    public class Pool<T> : IDisposable where T : IDisposable
    {
        public int MaxCount
        {
            get { return maxCount; }
            set { maxCount =  value; }
        }

        public int Count
        {
            get { return count; }
        }

        public Func<T> ObjectFactory { get; set; }
        public Func<T,bool> ObjectActive { get; set; }

        private readonly ConcurrentBag<T> bag = new ConcurrentBag<T>();
        private readonly AutoResetEvent notFullEvent = new AutoResetEvent(false);
        private int maxCount = 32;
        private int count = 0;


        private bool Active(T value)
        {
            if (ObjectActive == null) return true;
            return ObjectActive(value);
        }

        public T Borrow()
        {
            T value;
            bool ok = bag.TryTake(out value);
            if (ok)
            {
                if (Active(value))
                {
                    return value;
                }
                else
                {
                    HandleInactive(value);
                    return Borrow();
                }
            }
            if (count < maxCount)
            {
                value = ObjectFactory();
                Interlocked.Increment(ref count);
                return value;
            }
            notFullEvent.WaitOne();
            return Borrow();

        }


        public void Return(T value)
        {
            if (value == null) return;

            if (!Active(value))
            {
                HandleInactive(value);
            }
            else
            {
                bag.Add(value);
                notFullEvent.Set();
            }
        }

        public T Create()
        {
            return ObjectFactory();
        }

        private void HandleInactive(T value)
        {
            if (!Active(value))
            {
                value.Dispose();
                Interlocked.Decrement(ref count);
            }
        }

        public virtual void Dispose()
        {
            foreach (T value in bag)
            {
                value.Dispose();
            }
        }
    }
}
