using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public sealed partial class Grid : IDisposable
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Memory<Occupant> cells;
        private readonly IMemoryOwner<Occupant> owner;

        private Grid(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            int size = width * height;
            this.owner = MemoryPool<Occupant>.Shared.Rent(size);
            this.cells = owner.Memory.Slice(0, size);
            this.cells.Span.Fill(Occupant.None);
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
            this.cells.CopyTo(clone.cells);
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
            return cells.Span[Index(loc)];
        }

        public void Set(Loc loc, Occupant occ)
        {
            cells.Span[Index(loc)] = occ;
        }

        public bool IsVacant(Loc loc)
        {
            return Get(loc).Equals(Occupant.None);
        }

        public void Dispose()
        {
            owner.Dispose();
        }

        public bool Fall()
        {
            return GridFallHelper.Fall(this);
        }
    }
}
