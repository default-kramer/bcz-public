using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public static class Helpers
    {
        public static IReplayCollector Combine(this IReplayCollector first, IReplayCollector second)
        {
            return new ReplayModel.CompositeReplayCollector(first, second);
        }

        public static T Advance<T>(this IEnumerator<T> enumerator)
        {
            if (!enumerator.MoveNext())
            {
                throw new Exception("cannot advance enumerator");
            }
            return enumerator.Current;
        }
    }
}
