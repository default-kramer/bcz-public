using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public readonly ref struct GridSize
    {
        public readonly int Width;
        public readonly int Height;

        public GridSize(IReadOnlyGrid grid)
        {
            this.Width = grid.Width;
            this.Height = grid.Height;
        }
    }

    /// <summary>
    /// A read-only reference to a grid which might be mutated by someone else.
    /// </summary>
    public interface IReadOnlyGrid
    {
        int Width { get; }
        int Height { get; }
        GridSize Size { get; }
        Occupant Get(Loc loc);

        bool InBounds(Loc loc);

        bool IsVacant(Loc loc);

        ReadOnlySpan<Occupant> ToSpan();

        IImmutableGrid MakeImmutable();

        int HashGrid();

#if DEBUG
        string PrintGrid { get; }

        bool CheckGridString(params string[] rows);
#endif
    }

    /// <summary>
    /// Unlike the <see cref="IReadOnlyGrid"/>, this grid cannot be mutated by anyone.
    /// (The implementing class must enforce this.)
    /// </summary>
    public interface IImmutableGrid : IReadOnlyGrid { }

    public readonly struct GridStats
    {
        public readonly int EnemyCount;
        public readonly int OccupantCount;

        public GridStats(Grid grid)
        {
            EnemyCount = 0;
            OccupantCount = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var occ = grid.Get(new Loc(x, y));
                    switch (occ.Kind)
                    {
                        case OccupantKind.Enemy:
                            EnemyCount++;
                            OccupantCount++;
                            break;
                        case OccupantKind.None:
                            break;
                        default:
                            OccupantCount++;
                            break;
                    }
                }
            }
        }
    }

    public abstract class GridBase : IReadOnlyGrid
    {
        protected readonly Occupant[] cells;
        public readonly int Width;
        public readonly int Height;

        protected GridBase(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentException($"Invalid width/height: {width}, {height}");
            }
            this.Width = width;
            this.Height = height;
            this.cells = new Occupant[width * height];
        }

        protected GridBase(IReadOnlyGrid copyFrom)
        {
            this.Width = copyFrom.Width;
            this.Height = copyFrom.Height;
            this.cells = copyFrom.ToSpan().ToArray();
        }

        int IReadOnlyGrid.Width => Width;

        int IReadOnlyGrid.Height => Height;

        public GridSize Size => new GridSize(this);

        public Occupant Get(Loc loc)
        {
            return cells[Index(loc)];
        }

        public bool InBounds(Loc loc)
        {
            return loc.X >= 0 && loc.Y >= 0 && loc.X < Width && loc.Y < Height;
        }

        public int Index(Loc loc)
        {
            return loc.Y * Width + loc.X;
        }

        public Loc Loc(int index)
        {
            return new Loc(index % Width, index / Width);
        }

        public bool IsVacant(Loc loc)
        {
            return Get(loc) == Occupant.None;
        }

        public ReadOnlySpan<Occupant> ToSpan()
        {
            return cells.AsSpan();
        }

        public abstract IImmutableGrid MakeImmutable();

        /// <summary>
        /// These hashes are inserted into replays for error detection.
        /// </summary>
        public int HashGrid()
        {
            int hash = 17;
            for (int i = 0; i < cells.Length; i++)
            {
                hash = hash * 23 + cells[i].GetHashCode();
            }
            return hash;
        }

#if DEBUG
        const string Newline = "\n";

        public string PrintGrid
        {
            get
            {
                var sb = new StringBuilder();
                for (int y = Height - 1; y >= 0; y--)
                {
                    sb.Append(Newline);
                    for (int x = 0; x < Width; x++)
                    {
                        var occ = Get(new Loc(x, y));
                        sb.Append(PrintOcc(occ)).Append(" ");
                    }
                }
                return sb.ToString();
            }
        }

        private static string PrintOcc(Occupant occ)
        {
            if (occ.Kind == OccupantKind.None)
            {
                return "  ";
            }
            if (occ.Kind == OccupantKind.Enemy && occ.Color == Color.Blank)
            {
                return "[]";
            }

            string x = occ.Color switch
            {
                Color.Red => "r",
                Color.Yellow => "y",
                Color.Blue => "b",
                Color.Blank => "o",
                _ => throw new Exception("TODO: " + occ.Color),
            };

            if (occ.Kind == OccupantKind.Enemy)
            {
                x = x.ToUpperInvariant();
            }

            return occ.Direction switch
            {
                Direction.Left => $"{x}>",
                Direction.Right => $"<{x}",
                _ => $"{x}{x}",
            };
        }

        public bool CheckGridString(params string[] rows)
        {
            string expected = rows[0];
            if (rows.Length > 1)
            {
                expected = string.Join(Newline, rows);
            }
            expected = expected.Replace("\r\n", Newline);
            if (expected.StartsWith(Newline))
            {
                expected = expected.Substring(Newline.Length);
            }
            var actual = PrintGrid;
            return actual.EndsWith(expected);
        }
#endif
    }

    public sealed partial class Grid : GridBase
    {
        private readonly GridFallHelper.BlockedFlag[] blockedFlagBuffer;
        private readonly bool[] assumeUnblockedBuffer;
        private readonly GridDestroyHelper.Group[] groupsBuffer;
        private GridStats? stats = null; // null when recalculation is needed

        public GridStats Stats
        {
            get
            {
                stats = stats ?? new GridStats(this);
                return stats.Value;
            }
        }

        private Grid(int width, int height) : base(width, height)
        {
            int size = cells.Length;
            this.blockedFlagBuffer = new GridFallHelper.BlockedFlag[size];
            this.assumeUnblockedBuffer = new bool[size];
            this.groupsBuffer = new GridDestroyHelper.Group[size];
        }

        public static Grid Create(int width, int height)
        {
            return new Grid(width, height);
        }

        public const int DefaultWidth = 8;
        public const int DefaultHeight = 20;

        public static Grid Create(ISinglePlayerSettings settings, PRNG prng)
        {
            var grid = Grid.Create(settings.GridWidth, settings.GridHeight);
            GridCreateHelper.SetupGrid(grid, prng, settings.EnemyCount, settings.EnemiesPerStripe, settings.RowsPerStripe);
            return grid;
        }

        public static Grid Clone(IReadOnlyGrid copyFrom)
        {
            var grid = new Grid(copyFrom.Width, copyFrom.Height);
            copyFrom.ToSpan().CopyTo(grid.cells);
            return grid;
        }

        public override IImmutableGrid MakeImmutable()
        {
            return new ImmutableGrid(this);
        }

        public void Set(Loc loc, Occupant occ)
        {
            stats = null;
            cells[Index(loc)] = occ;
        }

        /// <summary>
        /// Set the given <paramref name="loc"/> to <paramref name="newOcc"/>.
        /// If the previous occupant was paired, update its partner accordingly.
        /// </summary>
        public RevertInfo SetWithDivorce(Loc loc, Occupant newOcc)
        {
            stats = null;

            var index = Index(loc);
            var current = cells[index];
            var revertInfo = new RevertInfo(index, current, index, current);

            if (current.Kind == OccupantKind.Catalyst && current.Direction != Direction.None)
            {
                var otherLoc = loc.Neighbor(current.Direction);
                var otherIndex = Index(otherLoc);
                var otherOcc = cells[otherIndex];
                if (otherOcc.Kind == OccupantKind.Catalyst)
                {
                    revertInfo = new RevertInfo(index, current, otherIndex, otherOcc);
                    cells[otherIndex] = otherOcc.SetDirection(Direction.None);
                }
            }

            cells[index] = newOcc;
            return revertInfo;
        }

        public void Revert(RevertInfo r)
        {
            stats = null;
            cells[r.index1] = r.occupant1;
            cells[r.index2] = r.occupant2;
        }

        public readonly ref struct RevertInfo
        {
            public readonly int index1;
            public readonly Occupant occupant1;
            public readonly int index2;
            public readonly Occupant occupant2;

            public RevertInfo(int index1, Occupant occupant1, int index2, Occupant occupant2)
            {
                this.index1 = index1;
                this.occupant1 = occupant1;
                this.index2 = index2;
                this.occupant2 = occupant2;
            }
        }

        public bool Fall(Span<int> fallCountBuffer)
        {
            return GridFallHelper.Fall(this, blockedFlagBuffer, assumeUnblockedBuffer, fallCountBuffer);
        }

        internal bool Destroy(TickCalculations calculations)
        {
            return new GridDestroyHelper(this, groupsBuffer, calculations).Execute(this);
        }

        public void Burst()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var loc = new Loc(x, y);
                    var occ = Get(loc);
                    if (occ.Kind == OccupantKind.Catalyst && occ.Color == Color.Blank)
                    {
                        Set(loc, Occupant.None);
                    }
                }
            }
            GridDestroyHelper.PostDestroy(this);
        }

        private int CountEmptyBottomRows()
        {
            int y = 0;
            while (y < Height)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!IsVacant(new Loc(x, y)))
                    {
                        return y;
                    }
                }
                y++;
            }
            return y;
        }

        public int ShiftToBottom()
        {
            int count = CountEmptyBottomRows();
            if (count == 0)
            {
                return count;
            }

            for (int y = 0; y < Height; y++)
            {
                int y2 = y + count;
                if (y2 < Height)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Set(new Loc(x, y), Get(new Loc(x, y2)));
                    }
                }
                else
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Set(new Loc(x, y), Occupant.None);
                    }
                }
            }

            return count;
        }
    }

    public sealed class ImmutableGrid : GridBase, IImmutableGrid
    {
        public ImmutableGrid(IReadOnlyGrid copyFrom) : base(copyFrom) { }

        public override IImmutableGrid MakeImmutable()
        {
            return this;
        }
    }
}
