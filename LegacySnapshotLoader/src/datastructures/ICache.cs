using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegacyBlockLoader.Datastructures
{
    internal interface  ICache<T1, T2>
    {
        T2 Get(T1 key);

        void Put(T1 key, T2 value);

        bool TryGetValue(T1 key, out T2 value);

        void Clear();
    }
}
