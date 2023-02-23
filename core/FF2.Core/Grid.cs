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

        public GridSize(int w, int h)
        {
            this.Width = w;
            this.Height = h;
        }

        public int LocIndex(Loc loc)
        {
            return loc.Y * Width + loc.X;
        }
    }

    public interface IReadOnlyGridSlim
    {
        GridSize Size { get; }
        Occupant Get(Loc loc);
    }

    /// <summary>
    /// A read-only reference to a grid which might be mutated by someone else.
    /// </summary>
    public interface IReadOnlyGrid : IReadOnlyGridSlim
    {
        int Width { get; }
        int Height { get; }

        bool InBounds(Loc loc);

        bool IsVacant(Loc loc);

        Mover NewMover(SpawnItem item);

        ReadOnlySpan<Occupant> ToSpan();

        IImmutableGrid MakeImmutable();

        int HashGrid();

#if DEBUG
        /// <summary>
        /// Just for tests. This is a property so you can easily grab it when debugging.
        /// </summary>
        string PrintGrid { get; }

        /// <summary>
        /// Just for tests. Returns "ok" if everything matches.
        /// Otherwise returns a diff that should print well in test output.
        /// Note: If <paramref name="rows"/> is exactly one string which contains any newlines,
        /// we assume the caller is passing multiple rows separated by newlines.
        /// </summary>
        string DiffGridString(params string[] rows);
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

        protected virtual void Put(Loc loc, Occupant occupant)
        {
            cells[Index(loc)] = occupant;
        }

        public bool InBounds(Loc loc)
        {
            return loc.X >= 0 && loc.Y >= 0 && loc.X < Width && loc.Y < Height;
        }

        public int Index(Loc loc)
        {
            return loc.Y * Width + loc.X;
        }

        public Mover NewMover(SpawnItem item)
        {
            if (item.IsCatalyst(out var occs))
            {
                var locA = new Loc(Width / 2 - 1, 0);
                var locB = locA.Neighbor(Direction.Right);
                return new Mover(locA, occs.left, locB, occs.right);
            }
            else
            {
                throw new Exception("TODO hmm...");
            }
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

        public void Clear()
        {
            cells.AsSpan().Fill(Occupant.None);
        }

#if DEBUG
        const string Newline = "\n";

        private string BuildGridString()
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

        public string PrintGrid => BuildGridString();

        public static char GetLowercase(Color color)
        {
            return color switch
            {
                Color.Red => 'r',
                Color.Yellow => 'y',
                Color.Blue => 'b',
                Color.Blank => 'o',
                _ => throw new Exception("TODO: " + color),
            };
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

            char c = GetLowercase(occ.Color);

            string str = occ.Direction switch
            {
                Direction.Left => $"{c}>",
                Direction.Right => $"<{c}",
                _ => $"{c}{c}",
            };

            if (occ.Kind == OccupantKind.Enemy)
            {
                str = str.ToUpperInvariant();
            }

            return str;
        }

        public string DiffGridString(params string[] expectedRows)
        {
            if (expectedRows.Length == 1 && expectedRows.Single().Contains("\n"))
            {
                expectedRows = expectedRows.Single().Split(Lists.Newlines, StringSplitOptions.RemoveEmptyEntries);
            }
            if (expectedRows.Length == 0)
            {
                return "No expectations given";
            }

            var gridString = BuildGridString();
            var actualRows = gridString.Split(Lists.Newlines, StringSplitOptions.RemoveEmptyEntries);

            if (expectedRows.Length > actualRows.Length)
            {
                return $"Expected at least {expectedRows.Length} rows, but grid only has {actualRows.Length}";
            }

            var diff = new StringBuilder();
            bool isDifferent = false;
            int extra = actualRows.Length - expectedRows.Length;

            for (int i = 0; i < expectedRows.Length; i++)
            {
                string expectedRow = expectedRows[i];
                string actualRow;
                int actualIndex = i + extra;
                if (actualIndex < actualRows.Length && actualIndex > -1)
                {
                    actualRow = actualRows[actualIndex];
                }
                else
                {
                    actualRow = "<< out of bounds >>";
                }
                string prefix = "  ";
                if (expectedRow != actualRow)
                {
                    prefix = "! ";
                    isDifferent = true;
                }
                // The leading newline is important for unit test output
                diff.AppendLine().Append($"{prefix}Expected |{expectedRow}| Actual |{actualRow}|");
            }

            if (isDifferent)
            {
                return diff.ToString();
            }

            for (int i = 0; i < extra; i++)
            {
                if (!string.IsNullOrWhiteSpace(actualRows[i]))
                {
                    return "ok:partial";
                }
            }

            return "ok";
        }
#endif
    }

    public sealed partial class Grid : GridBase
    {
        private readonly GridFallHelper.BlockedFlag[] blockedFlagBuffer;
        private readonly bool[] assumeUnblockedBuffer;
        private readonly GridDestroyHelper.Group[] groupsBuffer;
        private GridStats? stats = null; // null when recalculation is needed
        private readonly DestructionCalculations tickCalculations;

        public ITickCalculations TickCalc => tickCalculations;

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
            this.tickCalculations = new DestructionCalculations(this);
        }

        public static Grid Create(int width, int height)
        {
            return new Grid(width, height);
        }

        public static Grid Create()
        {
            return Create(DefaultWidth, DefaultHeight);
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

        public void CopyFrom(IReadOnlyGrid copyFrom)
        {
            copyFrom.ToSpan().CopyTo(this.cells);
        }

        public override IImmutableGrid MakeImmutable()
        {
            return new ImmutableGrid(this);
        }

        public void Set(Loc loc, Occupant occ)
        {
            stats = null;
            Put(loc, occ);
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

        public bool FallCompletely(Span<int> fallCountBuffer)
        {
            bool retval = Fall(fallCountBuffer);
            while (Fall(fallCountBuffer)) { }
            return retval;
        }

        public bool Fall(Span<int> fallCountBuffer)
        {
            return GridFallHelper.Fall(this, blockedFlagBuffer, assumeUnblockedBuffer, fallCountBuffer);
        }

        /// <summary>
        /// Test whether <see cref="Fall"/> will return true without mutating the grid.
        /// </summary>
        public bool CanFall()
        {
            return GridFallHelper.CanFall(this, blockedFlagBuffer, assumeUnblockedBuffer);
        }

        internal void ResetDestructionCalculations()
        {
            tickCalculations.Reset();
        }

        /// <summary>
        /// For testing only
        /// </summary>
        public ITickCalculations Test_FindGroups(int matchCount)
        {
            tickCalculations.Reset();
            new GridDestroyHelper(this, groupsBuffer, tickCalculations).FindGroups(matchCount, tickCalculations);
            return tickCalculations;
        }

        internal bool Destroy(ref ComboInfo info)
        {
            tickCalculations.Reset();
            bool result = new GridDestroyHelper(this, groupsBuffer, tickCalculations).Execute(this);
            if (result)
            {
                info = info.AfterDestruction(tickCalculations);
            }
            return result;
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

        public int CountEmptyBottomRows()
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

        public int ShiftDown(int count)
        {
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

        /// <summary>
        /// Place the given <paramref name="move"/> onto the current grid, or return false if there is no room.
        /// The new occupants will be positioned as if the user performed a plummet.
        /// You can follow this up with calls to Burst() and Fall() if you want to simulate a burst.
        /// Then you are ready to start the destroy/fall cycle.
        /// </summary>
        public bool Place(Move move)
        {
            var mover = NewMover(move.SpawnItem);
            mover = mover.JumpTo(move.Orientation);
            mover = mover.ToTop(Height);
            var plummet = mover.PreviewPlummet(this);
            if (plummet.HasValue)
            {
                mover = plummet.Value;
                Set(mover.LocA, mover.OccA);
                Set(mover.LocB, mover.OccB);
                return true;
            }
            return false;
        }
    }

    public sealed class ImmutableGrid : GridBase, IImmutableGrid
    {
        public ImmutableGrid(IReadOnlyGrid copyFrom) : base(copyFrom) { }

        public override IImmutableGrid MakeImmutable()
        {
            return this;
        }

        protected override void Put(Loc loc, Occupant occupant)
        {
            throw new InvalidOperationException("This method should be uncallable (without reflection)");
        }
    }
}
