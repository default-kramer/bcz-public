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
    }
}
