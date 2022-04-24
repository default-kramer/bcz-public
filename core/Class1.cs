/*
 * Chess engines have an easy way to Make and Unmake a move.
 * All you need to store is (from-loc, to-loc, captured-piece).
 * But this game does not have that property.
 * Can I think of a clever way to resolve the prespawn cycle without having to clone the grid?
 * Do I even need to?
 * One way to do it would be to model that cycle as many moves. For example
 *   - (destroy occ loc)
 *   - (fall occ from to)
 * Modeling it this way might have other benefits...
 */

using System;
using System.Buffers;
using Bits = System.UInt32;

namespace FF2.Core
{
    public enum OccupantKind : Bits
    {
        None = 0,
        Enemy = 1,
        Catalyst = 2,
        // reserve 3-F for the future
        All = Enemy | Catalyst,
    }

    public enum Color : Bits
    {
        Blank = 0,
        Red = 0x10,
        Yellow = 0x20,
        Blue = Red | Yellow,
        All = Red | Yellow | Blue,
    }

    public enum Direction : Bits
    {
        None = 0,
        Up = 0x40,
        Right = 0x80,
        Down = 0x100,
        Left = 0x200,
        All = Up | Right | Down | Left
    }

    static class EnumHelpers
    {
        const Bits Color1 = (Bits)Color.All;
        const Bits Color0 = Bits.MaxValue ^ Color1;
        const Bits Kind1 = (Bits)OccupantKind.All;
        const Bits Kind0 = Bits.MaxValue ^ Kind1;
        const Bits Direction1 = (Bits)Direction.All;
        const Bits Direction0 = Bits.MaxValue ^ Direction1;

        public static Color GetColor(this Bits val)
        {
            return (Color)(val & Color1);
        }

        public static Bits SetColor(this Bits val, Color color)
        {
            return (val & Color0) | (Bits)color;
        }

        public static OccupantKind GetKind(this Bits val)
        {
            return (OccupantKind)(val & Kind1);
        }

        public static Bits SetKind(this Bits val, OccupantKind kind)
        {
            return (val & Kind0) | (Bits)kind;
        }

        public static Direction GetDirection(this Bits val)
        {
            return (Direction)(val & Direction1);
        }
    }

    public struct Occupant
    {
        private readonly Bits data;

        public static readonly Occupant None = new Occupant();

        public Occupant(OccupantKind kind, Color color, Direction direction)
        {
            this.data = (Bits)kind | (Bits)color | (Bits)direction;
        }

        private Occupant(Bits data)
        {
            this.data = data;
        }

        public Color Color { get { return data.GetColor(); } }

        public Occupant SetColor(Color color)
        {
            return new Occupant(this.data.SetColor(color));
        }

        public OccupantKind Kind { get { return data.GetKind(); } }

        public Direction Direction { get { return data.GetDirection(); } }
    }

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
    }

    public sealed class Grid : IDisposable
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

        public void Dispose()
        {
            owner.Dispose();
        }

        public bool Fall()
        {
            return GridFallAlgorithm.Fall(this);
        }
    }

    static class GridFallAlgorithm
    {
        enum BlockedFlag : int
        {
            Unsure = 0,
            Blocked = 1,
            Unblocked = 2,
        }

        public static bool Fall(Grid grid)
        {
            int size = grid.Width * grid.Height;

            using var blockedFlagOwner = MemoryPool<BlockedFlag>.Shared.Rent(size);
            var results = blockedFlagOwner.Memory.Slice(0, size).Span;
            results.Fill(BlockedFlag.Unsure);

            using var assumeUnblockedOwner = MemoryPool<bool>.Shared.Rent(size);
            var assumeUnblocked = assumeUnblockedOwner.Memory.Slice(0, size).Span;
            assumeUnblocked.Fill(false);

            CalcBlocked(grid, results, assumeUnblocked);

            return ApplyResults(grid, results);
        }

        private static void CalcBlocked(Grid grid, Span<BlockedFlag> results, Span<bool> assumeUnblocked)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    bool blocked = IsBlocked(grid, results, assumeUnblocked, loc);
                    results[grid.Index(loc)] = blocked ? BlockedFlag.Blocked : BlockedFlag.Unblocked;
                }
            }
        }

        /// <summary>
        /// Determine whether the given <param name="loc"> is blocked.
        /// Some trivial cases are
        /// * a vacant loc is unblocked
        /// * a normal enemy is blocked (it never moves)
        /// The non-trivial case is to assume that the loc is unblocked and recursively check
        /// that all of the <see cref="FallDependencies(Occupant)"/> are also unblocked.
        /// </summary>
        /// <param name="results">
        /// Contains the information we learned from checking previous Locs.
        /// This is read-only because it should only be updated when <paramref name="assumeUnblocked"/> is empty,
        /// which is done at the site of the outermost (non-recursive) call.
        /// </param>
        private static bool IsBlocked(Grid grid, ReadOnlySpan<BlockedFlag> results, Span<bool> assumeUnblocked, Loc loc)
        {
            if (!grid.InBounds(loc))
            {
                return true; // can't fall off the grid
            }

            var idx = grid.Index(loc);
            if (assumeUnblocked[idx])
            {
                return false;
            }
            switch (results[idx])
            {
                case BlockedFlag.Blocked: return true;
                case BlockedFlag.Unblocked: return false;
            }

            var occ = grid.Get(loc);
            var kind = occ.Kind;
            switch (kind)
            {
                case OccupantKind.None:
                    return false;
                // This code should support falling Enemies. Set their Direction==Down and try it out.
                case OccupantKind.Enemy:
                case OccupantKind.Catalyst:
                    var temp = assumeUnblocked[idx];
                    assumeUnblocked[idx] = true;

                    bool isBlocked = false;
                    var deps = FallDependencies(occ);
                    foreach (var dir in Lists.Directions.DRLU)
                    {
                        if (deps.HasFlag(dir))
                        {
                            isBlocked = isBlocked || IsBlocked(grid, results, assumeUnblocked, loc.Neighbor(dir));
                        }
                    }

                    assumeUnblocked[idx] = temp;
                    return deps != Direction.None && isBlocked;
                default:
                    throw new InvalidOperationException("unexpected OccupantKind: " + kind);
            }
        }

        /// <summary>
        /// Returns all the Directions (as a single enum value) that must be unblocked
        /// in order for the given Occupant to fall.
        /// </summary>
        static Direction FallDependencies(Occupant occ)
        {
            var dir = occ.Direction;
            switch (occ.Kind)
            {
                case OccupantKind.Catalyst:
                    dir = dir | Direction.Down;
                    break;
            }

            return dir;
        }

        static bool ApplyResults(Grid grid, ReadOnlySpan<BlockedFlag> results)
        {
            bool any = false;

            for (int y = 1; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var occ = grid.Get(loc);

                    if (occ.Kind != OccupantKind.None
                        && results[grid.Index(loc)] == BlockedFlag.Unblocked)
                    {
                        grid.Set(loc.Neighbor(Direction.Down), occ);
                        grid.Set(loc, Occupant.None);
                        any = true;
                    }
                }
            }

            return any;
        }
    }
}
