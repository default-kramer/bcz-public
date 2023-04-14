using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
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

        public Move MakeBurst()
        {
            return new Move(Orientation, SpawnItem, true);
        }

        public override string ToString()
        {
            return $"(Move {SpawnItem} {Orientation} {DidBurst})";
        }
    }
}
