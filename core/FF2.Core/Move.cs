using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public readonly struct Move
    {
        public readonly Orientation Orientation;
        public readonly SpawnItem SpawnItem;
        public readonly bool DidBurst;

        public Move(Orientation orientation, SpawnItem spawnItem, bool didBurst)
        {
            this.Orientation = orientation;
            this.SpawnItem = spawnItem;
            this.DidBurst = didBurst;
        }
    }
}
