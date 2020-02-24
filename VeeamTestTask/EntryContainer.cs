using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace VeeamTestTask
{
    /// <summary>
    /// Represents a thread safe collection of integer index/byte array pair that counts withdrawals
    /// </summary>
    public class EntryContainer
    {
        private AutoResetEvent _pauseEvent = new AutoResetEvent(false);
        /// <summary>
        /// Limiter of <see cref="EntryContainer"/> size. Can be used to block adding new items until some old will be removed
        /// </summary>
        public int SizeLimiter { get; private set; }
        private ConcurrentDictionary<int, byte[]> Entries { get; set; } = new ConcurrentDictionary<int, byte[]>();
        public int Count => Entries.Count;
        public int WithdrawalsCount { get; private set; } = 0;
        /// <summary>
        /// Creates new instance of <see cref="EntryContainer"/> with counter that can be used to limit <see cref="EntryContainer"/> size
        /// </summary>
        /// <param name="sizeLimiter">Limiter of size</param>
        public EntryContainer(int sizeLimiter)
        {
            SizeLimiter = sizeLimiter;
        }
        /// <summary>
        /// Add an integer index/byte array pair to <see cref="EntryContainer"/>
        /// </summary>
        /// <param name="index">Integer index</param>
        /// <param name="item">Byte array item</param>
        public void Add(int index, byte[] item)
        {
            if (!Entries.TryAdd(index, item))
                throw new InvalidOperationException(string.Format("Item with index {0} was not added", index));
        }
        /// <summary>
        /// Attempts to get byte array to <paramref name="item"/> param by its <paramref name="index"/> and then remove it from <see cref="EntryContainer"/>
        /// </summary>
        /// <param name="index">Integer key</param>
        /// <param name="item">Byte array item</param>
        /// <returns>True if <paramref name="index"/> exists and item was got; false if <paramref name="index"/> does not exists in <see cref="EntryContainer"/></returns>
        public bool TryGet(int index, out byte[] item)
        {
            if (Entries.ContainsKey(index))
            {
                Entries.TryRemove(index, out item);
                WithdrawalsCount++;
                if (Count < SizeLimiter)
                {
                    _pauseEvent.Set();
                }
                return true;
            }
            item = default;
            return false;
        }
        /// <summary>
        /// Waits until count of <see cref="EntryContainer"/> becomes less then <see cref="SizeLimiter"/>
        /// </summary>
        public void WaitUntilCountBecomeLess()
        {
            _pauseEvent.WaitOne();
        }
    }
}
