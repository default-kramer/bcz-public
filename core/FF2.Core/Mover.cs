using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// Immutable. Tracks the 2 Occupants that the player has control of.
    /// </summary>
    public readonly struct Mover
    {
        public readonly Loc LocA;
        public readonly Loc LocB;
        public readonly Occupant OccA;
        public readonly Occupant OccB;

        public Mover(Loc locA, Occupant occA, Loc locB, Occupant occB)
        {
            this.LocA = locA;
            this.OccA = occA;
            this.LocB = locB;
            this.OccB = occB;
        }

        public Orientation Orientation => new Orientation(OccA.Direction, LocA.X);

        public Move GetMove(bool didBurst)
        {
            return new Move(Orientation, new SpawnItem(OccA.Color, OccB.Color), didBurst);
        }

        /// <summary>
        /// Relocates the Y coordinates to the maximum allowed by the given <paramref name="gridHeight"/>.
        /// </summary>
        /// <remarks>
        /// We usually keep Min(LocA.Y, LocB.Y) == 0 for as long as possible.
        /// </remarks>
        public Mover ToTop(int gridHeight)
        {
            int yDelta = Math.Min(gridHeight - LocA.Y, gridHeight - LocB.Y) - 1;
            return new Mover(LocA.Add(0, yDelta), OccA, LocB.Add(0, yDelta), OccB);
        }

        /// <summary>
        /// Returns a new mover indicating where this mover would land if it were dropped
        /// onto the given grid. Returns null if the grid cannot accept this mover.
        /// </summary>
        public Mover? PreviewPlummet(IReadOnlyGrid grid)
        {
            var top = ToTop(grid.Height);
            var a = top.LocA;
            var b = top.LocB;

            while (a.Y >= grid.Height || b.Y >= grid.Height ||
                (a.Y >= 0 && b.Y >= 0 && grid.IsVacant(a) && grid.IsVacant(b)))
            {
                a = a.Neighbor(Direction.Down);
                b = b.Neighbor(Direction.Down);
            }

            a = a.Neighbor(Direction.Up);
            b = b.Neighbor(Direction.Up);

            var result = new Mover(a, OccA, b, OccB);
            if (grid.InBounds(result.LocA) && grid.InBounds(result.LocB))
            {
                return result;
            }
            return null;
        }

        public Occupant? GetOcc(Loc loc)
        {
            if (loc == LocA) { return OccA; }
            if (loc == LocB) { return OccB; }
            return null;
        }

        /// <summary>
        /// Handles the player's left/right commands.
        /// </summary>
        public Mover? Translate(Direction dir, int gridWidth)
        {
            var a = LocA.Neighbor(dir);
            var b = LocB.Neighbor(dir);
            if (a.X >= 0 && b.X >= 0 && a.X < gridWidth && b.X < gridWidth)
            {
                return new Mover(a, OccA, b, OccB);
            }
            return null;
        }

        /// <summary>
        /// Handles the player's rotate cw/ccw commands.
        /// </summary>
        public Mover Rotate(bool clockwise, int gridWidth)
        {
            int ax;
            int ay;
            int bx;
            int by;
            Direction aDir;
            Direction bDir;

            if (clockwise)
            {
                switch (OccB.Direction)
                {
                    case Direction.Left:
                        ax = LocA.X;
                        ay = LocA.Y + 1;
                        bx = LocA.X;
                        by = LocA.Y;
                        aDir = Direction.Down;
                        bDir = Direction.Up;
                        break;
                    case Direction.Up:
                        ax = LocB.X + 1;
                        ay = LocB.Y;
                        bx = LocB.X;
                        by = LocB.Y;
                        aDir = Direction.Left;
                        bDir = Direction.Right;
                        break;
                    case Direction.Right:
                        ax = LocB.X;
                        ay = LocB.Y;
                        bx = LocB.X;
                        by = LocB.Y + 1;
                        aDir = Direction.Up;
                        bDir = Direction.Down;
                        break;
                    case Direction.Down:
                        ax = LocA.X;
                        ay = LocA.Y;
                        bx = LocA.X + 1;
                        by = LocA.Y;
                        aDir = Direction.Right;
                        bDir = Direction.Left;
                        break;
                    default:
                        throw new Exception("unexpected direction: " + OccB.Direction);
                }
            }
            else
            {
                switch (OccB.Direction)
                {
                    case Direction.Left:
                        ax = LocA.X;
                        ay = LocA.Y;
                        bx = LocA.X;
                        by = LocA.Y + 1;
                        aDir = Direction.Up;
                        bDir = Direction.Down;
                        break;
                    case Direction.Up:
                        ax = LocB.X;
                        ay = LocB.Y;
                        bx = LocB.X + 1;
                        by = LocB.Y;
                        aDir = Direction.Right;
                        bDir = Direction.Left;
                        break;
                    case Direction.Right:
                        ax = LocB.X;
                        ay = LocB.Y + 1;
                        bx = LocB.X;
                        by = LocB.Y;
                        aDir = Direction.Down;
                        bDir = Direction.Up;
                        break;
                    case Direction.Down:
                        ax = LocA.X + 1;
                        ay = LocA.Y;
                        bx = LocA.X;
                        by = LocA.Y;
                        aDir = Direction.Left;
                        bDir = Direction.Right;
                        break;
                    default:
                        throw new Exception("unexpected direction: " + OccB.Direction);
                }
            }

            // wall kick if needed
            if (ax < 0 || bx < 0)
            {
                ax++;
                bx++;
            }
            else if (ax >= gridWidth || bx >= gridWidth)
            {
                ax--;
                bx--;
            }

            return new Mover(new Loc(ax, ay), OccA.SetDirection(aDir), new Loc(bx, by), OccB.SetDirection(bDir));
        }

        /// <summary>
        /// Warning: This can return an out-of-bounds mover.
        /// </summary>
        public Mover JumpTo(Orientation target)
        {
            var mover = this;
            while (mover.Orientation.Direction != target.Direction)
            {
                mover = mover.Rotate(true, gridWidth: int.MaxValue);
            }
            int dx = target.X - mover.Orientation.X;
            return new Mover(mover.LocA.Add(dx, 0), mover.OccA, mover.LocB.Add(dx, 0), mover.OccB);
        }

        /// <summary>
        /// Returns a command that will cause this item's <see cref="Orientation"/>
        /// to approach the <paramref name="target"/> orientation.
        /// Returns null when they are equal and no further commands are needed.
        /// </summary>
        public Command? Approach(Orientation target)
        {
            var me = this.Orientation;
            if (me.Direction != target.Direction)
            {
                var ccw = this.Rotate(clockwise: false, gridWidth: int.MaxValue);
                if (ccw.Orientation.Direction == target.Direction)
                {
                    return Command.RotateCCW;
                }
                return Command.RotateCW;
            }
            if (me.X < target.X)
            {
                return Command.Right;
            }
            if (me.X > target.X)
            {
                return Command.Left;
            }
            return null;
        }
    }
}
