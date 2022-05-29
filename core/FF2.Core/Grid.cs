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

    public sealed partial class Grid : IReadOnlyGrid, IDisposable
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Occupant[] cells;
        private readonly GridFallHelper.BlockedFlag[] blockedFlagBuffer;
        private readonly bool[] assumeUnblockedBuffer;
        private readonly GridDestroyHelper.Group[] groupsBuffer;

        int IReadOnlyGrid.Width => this.Width;
        int IReadOnlyGrid.Height => this.Height;

        private Grid(int width, int height)
        {
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

        public static Grid Create(PRNG rand)
        {
            const int gridHeight = 20;
            const int gridWidth = 8;

            var grid = Grid.Create(gridWidth, gridHeight);
            GridCreateHelper.SetupSimpleGrid(grid, rand);
            GridCreateHelper.SetupHardcodedGrid(grid);

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
            cells[Index(loc)] = occ;
        }

        public bool IsVacant(Loc loc)
        {
            return Get(loc) == Occupant.None;
        }

        public void Dispose() { }

        public bool Fall()
        {
            return GridFallHelper.Fall(this, blockedFlagBuffer, assumeUnblockedBuffer);
        }

        public bool Destroy(TickCalculations calculations)
        {
            return new GridDestroyHelper(this, groupsBuffer, calculations).Execute(this);
        }
    }
}
