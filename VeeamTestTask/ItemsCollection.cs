using System.Collections.Generic;

namespace VeeamTestTask
{
    public class ItemsCollection<T>
    {
        private Dictionary<int, T> Dictionary { get; set; } = new Dictionary<int, T>();
        public int Count => Dictionary.Count;
        public int EntryCount { get; private set; } = 0;

        public void Add(int index, T item)
        {
            Dictionary.Add(index, item);            
        }

        public bool TryGet(int index, out T item)
        {
            if (Dictionary.ContainsKey(index))
            {
                item = Dictionary[index];
                Dictionary.Remove(index);
                EntryCount++;
                return true;
            }
            item = default;
            return false;
        }
    }
}
