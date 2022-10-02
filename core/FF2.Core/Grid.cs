using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// A read-only reference to a grid which might be mutated by someone else.
    /// </summary>
    public interface IReadOnlyGrid
    {
        int Width { get; }
        int Height { get; }
        Occupant Get(Loc loc);

        int Index(Loc loc);

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

        public GridStats(Grid grid)
        {
            EnemyCount = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var occ = grid.Get(new Loc(x, y));
                    if (occ.Kind == OccupantKind.Enemy)
                    {
                        EnemyCount++;
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
                    if (occ.Kind != OccupantKind.None && occ.Color == Color.Blank)
                    {
                        Set(loc, Occupant.None);
                    }
                }
            }
            GridDestroyHelper.PostDestroy(this);
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
