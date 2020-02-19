using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace VeeamTestTask
{
    public class ItemsCollection<T>
    {
        private AutoResetEvent countLessMaxEvent = new AutoResetEvent(false);
        public int MaxCount { get; private set; }
        private ConcurrentDictionary<int, T> Dictionary { get; set; } = new ConcurrentDictionary<int, T>();
        public int Count => Dictionary.Count;
        public int EntryCount { get; private set; } = 0;

        public ItemsCollection(int maxCount){
            MaxCount = maxCount;
        }
        public void Add(int index, T item)
        {
            Dictionary.TryAdd(index, item);            
        }

        public bool TryGet(int index, out T item)
        {
            if (Dictionary.ContainsKey(index))
            {
                Dictionary.TryRemove(index, out item);
                EntryCount++;
                if (Count < MaxCount)
                    countLessMaxEvent.Set();
                return true;
            }
            item = default;
            return false;
        }
        public void WaitUntilCountBecomeLess()
        {
            countLessMaxEvent.WaitOne();
        }
    }
}
