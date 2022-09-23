using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public interface IReadOnlyGrid
    {
        int Width { get; }
        int Height { get; }
        Occupant Get(Loc loc);

        int Index(Loc loc);

        bool InBounds(Loc loc);

        bool IsVacant(Loc loc);
    }

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

    public sealed partial class Grid : IReadOnlyGrid, IDisposable
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Occupant[] cells;
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

        int IReadOnlyGrid.Width => this.Width;
        int IReadOnlyGrid.Height => this.Height;

        private Grid(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentException($"Invalid width/height: {width}, {height}");
            }

            this.Width = width;
            this.Height = height;
            int size = width * height;
            this.cells = new Occupant[size];
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

        public Grid Clone()
        {
            var clone = new Grid(this.Width, this.Height);
            this.cells.AsSpan().CopyTo(clone.cells);
            return clone;
        }

        public int Index(Loc loc)
        {
            return loc.Y * Width + loc.X;
        }

        public bool InBounds(Loc loc)
        {
            return loc.X >= 0 && loc.Y >= 0 && loc.X < Width && loc.Y < Height;
        }

        public Occupant Get(Loc loc)
        {
            return cells[Index(loc)];
        }

        public void Set(Loc loc, Occupant occ)
        {
            stats = null;
            cells[Index(loc)] = occ;
        }

        public bool IsVacant(Loc loc)
        {
            return Get(loc) == Occupant.None;
        }

        public void Dispose() { }

        public bool Fall(Span<int> fallCountBuffer)
        {
            return GridFallHelper.Fall(this, blockedFlagBuffer, assumeUnblockedBuffer, fallCountBuffer);
        }

        public bool Destroy(TickCalculations calculations)
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
        public string PrintGrid
        {
            get
            {
                var sb = new StringBuilder();
                for (int y = Height - 1; y >= 0; y--)
                {
                    sb.AppendLine();
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
#endif
    }
}
