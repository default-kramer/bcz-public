using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public sealed class NullReplayCollector : IReplayCollector
    {
        public void Collect(Stamped<Command> command) { }

        public static readonly NullReplayCollector Instance = new NullReplayCollector();
    }
}
