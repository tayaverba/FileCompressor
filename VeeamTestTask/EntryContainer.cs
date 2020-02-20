using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace VeeamTestTask
{
    public class EntryContainer
    {
        private AutoResetEvent _pauseEvent = new AutoResetEvent(false);
        public int CountToPause { get; private set; }
        private ConcurrentDictionary<int, byte[]> Entries { get; set; } = new ConcurrentDictionary<int, byte[]>();
        public int Count => Entries.Count;
        public int EntryCount { get; private set; } = 0;

        public EntryContainer(int countToPause)
        {
            CountToPause = countToPause;
        }
        public void Add(int index, byte[] item)
        {
            Entries.TryAdd(index, item);            
        }

        public bool TryGet(int index, out byte[] item)
        {
            if (Entries.ContainsKey(index))
            {
                Entries.TryRemove(index, out item);
                EntryCount++;
                if (Count < CountToPause)
                {
                    _pauseEvent.Set();
                }
                return true;
            }
            item = default;
            return false;
        }
        public void WaitUntilCountBecomeLess()
        {
            _pauseEvent.WaitOne();
        }
    }
}
