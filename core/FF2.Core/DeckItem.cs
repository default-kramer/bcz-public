using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public readonly struct SpawnItem
    {
        public static readonly SpawnItem None = default(SpawnItem);
        public static readonly SpawnItem PENALTY = new SpawnItem(Occupant.IndestructibleEnemy, Occupant.IndestructibleEnemy);

        private readonly Occupant left;
        private readonly Occupant right;

        private SpawnItem(Occupant left, Occupant right)
        {
            this.left = left;
            this.right = right;
        }

        public static SpawnItem MakeCatalystPair(Color left, Color right)
        {
            var l = Occupant.MakeCatalyst(left, Direction.Right);
            var r = Occupant.MakeCatalyst(right, Direction.Left);
            return new SpawnItem(l, r);
        }

        public bool IsCatalyst(out (Occupant left, Occupant right) pair)
        {
            if (left.Kind == OccupantKind.Catalyst)
            {
                pair = (left, right);
                return true;
            }
            pair = (Occupant.None, Occupant.None);
            return false;
        }

        public bool IsPenalty()
        {
            return this.Equals(PENALTY);
        }

        public override string ToString()
        {
            return $"(SpawnItem {left} {right})";
        }
    }
}
