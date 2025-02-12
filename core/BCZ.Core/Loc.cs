using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    public readonly struct Loc
    {
        public readonly int X;
        public readonly int Y;

        public Loc(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public Loc Add(int x, int y)
        {
            return new Loc(this.X + x, this.Y + y);
        }

        public Loc Neighbor(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Add(0, 1);
                case Direction.Right:
                    return Add(1, 0);
                case Direction.Down:
                    return Add(0, -1);
                case Direction.Left:
                    return Add(-1, 0);
                default:
                    throw new ArgumentException("unexpected Direction: " + direction);
            }
        }

        public int ToIndex(IReadOnlyGridSlim grid)
        {
            return ToIndex(grid.Size);
        }

        public int ToIndex(GridSize size)
        {
            return Y * size.Width + X;
        }

        public static Loc FromIndex(int index, IReadOnlyGrid grid)
        {
            var width = grid.Width;
            return new Loc(index % width, index / width);
        }

        public static Loc FromIndex(int index, GridSize size)
        {
            var width = size.Width;
            return new Loc(index % width, index / width);
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

        public override string ToString()
        {
            return $"(Loc {X} {Y})";
        }
    }
}
