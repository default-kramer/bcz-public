using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public struct Loc
    {
        public readonly int X;
        public readonly int Y;

        public Loc(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public Loc Neighbor(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return new Loc(X, Y + 1);
                case Direction.Right:
                    return new Loc(X + 1, Y);
                case Direction.Down:
                    return new Loc(X, Y - 1);
                case Direction.Left:
                    return new Loc(X - 1, Y);
                default:
                    throw new ArgumentException("unexpected Direction: " + direction);
            }
        }

        public static bool operator ==(Loc a, Loc b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(Loc a, Loc b)
        {
            return !(a == b);
        }

        public override bool Equals(object? obj)
        {
            return obj is Loc other && this == other;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
