using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VeeamTestTask
{
    /// <summary>
    /// Represents a thread safe collection of integer index/byte array pair that counts withdrawals
    /// </summary>
    public class BlockContainer
    {
        private AutoResetEvent _pauseEvent = new AutoResetEvent(false);
        /// <summary>
        /// Limiter of <see cref="BlockContainer"/> size. Can be used to block adding new items until some old will be removed
        /// </summary>
        public int SizeLimiter { get; private set; }
        private ConcurrentDictionary<int, byte[]> Blocks { get; set; }
        public int Count => Blocks.Count;
        public int WithdrawalsCount { get; private set; } = 0;
        /// <summary>
        /// Creates new instance of <see cref="BlockContainer"/> with counter that can be used to limit <see cref="BlockContainer"/> size
        /// </summary>
        /// <param name="sizeLimiter">Limiter of size</param>
        public BlockContainer(int sizeLimiter)
        {
            Blocks = new ConcurrentDictionary<int, byte[]>();
            SizeLimiter = sizeLimiter;
        }
        /// <summary>
        /// Add an integer index/byte array pair to <see cref="BlockContainer"/>
        /// </summary>
        /// <param name="index">Integer index</param>
        /// <param name="item">Byte array item</param>
        public void Add(int index, byte[] item)
        {
            if (!Blocks.TryAdd(index, item))
                throw new InvalidOperationException(string.Format("Item with index {0} was not added", index));
        }
        /// <summary>
        /// Attempts to get byte array to <paramref name="item"/> param by its <paramref name="index"/> and then remove it from <see cref="BlockContainer"/>
        /// </summary>
        /// <param name="index">Integer key</param>
        /// <param name="item">Byte array item</param>
        /// <returns>True if <paramref name="index"/> exists and item was got; false if <paramref name="index"/> does not exists in <see cref="BlockContainer"/></returns>
        public bool TryGet(int index, out byte[] item)
        {
            if (Blocks.ContainsKey(index))
            {
                Blocks.TryRemove(index, out item);
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
        /// Waits until count of <see cref="BlockContainer"/> becomes less then <see cref="SizeLimiter"/>
        /// </summary>
        public void WaitUntilCountBecomeLess()
        {
            _pauseEvent.WaitOne();
        }
        /// <summary>
        /// Clears all container data and reset <see cref="WithdrawalsCount"/>
        /// </summary>
        public void Clear()
        {
            Blocks.Clear();
            WithdrawalsCount = 0;
            _pauseEvent.Set();
        }
    }
}
