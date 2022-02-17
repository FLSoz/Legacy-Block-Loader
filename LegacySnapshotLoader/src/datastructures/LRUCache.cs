using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegacyBlockLoader.Datastructures
{
    internal class LRUCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private int capacity = 5;
        private LinkedList<KeyValuePair<TKey, TValue>> elements;
        private Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> map;

        public LRUCache(int size) {
            this.capacity = size;
            elements = new LinkedList<KeyValuePair<TKey, TValue>>();
            map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
        }

        public void Clear()
        {
            elements.Clear();
            map.Clear();
        }

        public TValue Get(TKey key)
        {
            if (!map.ContainsKey(key)) {
                return default(TValue);
            }
            LinkedListNode <KeyValuePair<TKey, TValue>> node = map[key];
            elements.Remove(node);
            map[key] = elements.AddFirst(node.Value);
            return node.Value.Value;
        }

        public void Put(TKey key, TValue value)
        {
            if (map.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>> node))
            {
                elements.Remove(node);
                map[key] = elements.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            }
            else
            {
                if (elements.Count >= this.capacity)
                {
                    map.Remove(elements.Last().Key);
                    elements.RemoveLast();
                }
                map[key] = elements.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (map.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>> node))
            {
                elements.Remove(node);
                map[key] = elements.AddFirst(node.Value);
                value = node.Value.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }
    }
}
