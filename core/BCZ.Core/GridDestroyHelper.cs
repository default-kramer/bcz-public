using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    /// <summary>
    /// This struct should be very short-lived -- just create it, use it, and get rid of it
    /// within a single method.
    /// </summary>
    readonly struct GridDestroyHelper
    {
        private readonly IReadOnlyGrid grid;
        private readonly Group[] groups;
        private readonly DestructionCalculations calculations;

        public GridDestroyHelper(IReadOnlyGrid grid, Group[] groupsBuffer, DestructionCalculations calculations)
        {
            this.grid = grid;
            this.groups = groupsBuffer;
            this.calculations = calculations;

            groups.AsSpan().Fill(Group.None);
        }

        public bool Execute(Grid grid, int groupCount = 4)
        {
            if (!object.ReferenceEquals(this.grid, grid))
            {
                throw new ArgumentException("grid must be the same grid from the constructor...");
            }

            FindGroups(groupCount, calculations);

            return Destroy(grid, groupCount);
        }

        private bool Destroy(Grid grid, int groupCount)
        {
            bool anyDestroyed = false;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var index = grid.Index(loc);
                    var group = groups[index];

                    if (group.HorizontalCount >= groupCount || group.VerticalCount >= groupCount)
                    {
                        var destroyedOccupant = grid.Get(loc);
                        calculations.AddDestroyedOccupant(loc, destroyedOccupant, grid);
                        grid.Set(loc, Occupant.None);
                        anyDestroyed = true;
                    }
                }
            }

            if (anyDestroyed)
            {
                PostDestroy(grid);
            }

            return anyDestroyed;
        }

        /// <summary>
        /// Update catalysts whose partner was just destroyed.
        /// </summary>
        public static void PostDestroy(Grid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var occ = grid.Get(loc);
                    if (occ.Kind == OccupantKind.Catalyst)
                    {
                        var dir = occ.Direction;
                        if (dir != Direction.None)
                        {
                            var partnerLoc = loc.Neighbor(dir);
                            if (grid.InBounds(partnerLoc) && grid.IsVacant(partnerLoc)) // TODO I'm not sure why the bounds check is needed. Do I have a different bug?
                            {
                                occ = occ.SetDirection(Direction.None);
                                grid.Set(loc, occ);
                            }
                        }
                    }
                }
            }
        }

        internal void FindGroups(int groupCount, DestructionCalculations calculations)
        {
            // For each row, find runs Left-to-Right
            for (int y = 0; y < grid.Height; y++)
            {
                var cursor = new Loc(0, y);
                do
                {
                    cursor = HandleRun(cursor, Direction.Right, groupCount, calculations);
                } while (grid.InBounds(cursor));
            }

            // For each column, find runs Bottom-to-Top
            for (int x = 0; x < grid.Width; x++)
            {
                var cursor = new Loc(x, 0);
                do
                {
                    cursor = HandleRun(cursor, Direction.Up, groupCount, calculations);
                } while (grid.InBounds(cursor));
            }
        }

        private Loc HandleRun(Loc loc, Direction direction, int groupCount, DestructionCalculations calculations)
        {
            var occ = grid.Get(loc);
            var runColor = occ.Color;
            if (runColor == Color.Blank)
            {
                return loc.Neighbor(direction);
            }
            else
            {
                // Cursor will end up being 1 Loc beyond the end of the run, possibly out-of-bounds
                Loc cursor = loc.Neighbor(direction);
                int runCount = 1;
                bool hasEnemy = occ.Kind == OccupantKind.Enemy;

                while (grid.InBounds(cursor))
                {
                    var occ2 = grid.Get(cursor);
                    if (occ2.Color == runColor)
                    {
                        runCount++;
                        hasEnemy = hasEnemy || occ2.Kind == OccupantKind.Enemy;
                        cursor = cursor.Neighbor(direction);
                    }
                    else
                    {
                        break;
                    }
                }

                for (Loc iter = loc; iter != cursor; iter = iter.Neighbor(direction))
                {
                    var index = iter.ToIndex(grid);
                    groups[index] = groups[index].AdjustCount(direction, runCount);
                }

                if (runCount >= groupCount)
                {
                    if (direction == Direction.Up)
                    {
                        calculations.AddColumnDestruction(loc.X, hasEnemy);
                    }
                    else if (direction == Direction.Right)
                    {
                        calculations.AddRowDestruction(loc.Y, grid, hasEnemy);
                    }
                    else
                    {
                        throw new Exception("Assert fail: " + direction);
                    }
                }

                return cursor;
            }
        }

        public readonly struct Group
        {
            public readonly Color Color;
            public readonly int HorizontalCount;
            public readonly int VerticalCount;

            public static Group None = new Group(Color.Blank, 0, 0);

            public Group(Color color, int horizontalCount, int verticalCount)
            {
                this.Color = color;
                this.HorizontalCount = horizontalCount;
                this.VerticalCount = verticalCount;
            }

            public Group AdjustCount(Direction direction, int newCount)
            {
                switch (direction)
                {
                    case Direction.Left:
                    case Direction.Right:
                        return new Group(this.Color, newCount, this.VerticalCount);
                    case Direction.Up:
                    case Direction.Down:
                        return new Group(this.Color, this.HorizontalCount, newCount);
                    default:
                        throw new Exception("unexpected direction:" + direction);
                }
            }
        }
    }
}
