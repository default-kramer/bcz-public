using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    static class GridFallHelper
    {
        public enum BlockedFlag : int
        {
            Unsure = 0,
            Blocked = 1,
            Unblocked = 2,
        }

        /// <summary>
        /// Tries to move each occupant down by one cell. Returns true if anything moved.
        /// </summary>
        /// <param name="fallCounter">
        /// Keeps track of how far each occupant fell.
        /// For example, if something falls from (2,9) to (2,4) then the (2,4) location will have a drop value
        /// of 5 recorded (because it fell 5 cells).
        /// Because this method only moves by 1 cell at a time, the caller should initialize this buffer to all
        /// zeroes before the first call, but leave it untouched for any subsequent calls that are part of the same fall cycle.
        /// </param>
        public static bool Fall(Grid grid, Span<BlockedFlag> results, Span<bool> assumeUnblocked, Span<int> fallCounter)
        {
            Calculate(grid, results, assumeUnblocked);
            return ApplyResults(grid, results, fallCounter);
        }

        public static bool CanFall(IReadOnlyGrid grid, Span<BlockedFlag> results, Span<bool> assumeUnblocked)
        {
            Calculate(grid, results, assumeUnblocked);
            return TestResults(grid, results);
        }

        private static void Calculate(IReadOnlyGrid grid, Span<BlockedFlag> results, Span<bool> assumeUnblocked)
        {
            results.Fill(BlockedFlag.Unsure);
            assumeUnblocked.Fill(false);
            CalcBlocked(grid, results, assumeUnblocked);
        }

        private static void CalcBlocked(IReadOnlyGrid grid, Span<BlockedFlag> results, Span<bool> assumeUnblocked)
        {
            var size = grid.Size;

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    var loc = new Loc(x, y);
                    // IsBlocked(...) is a recursive function.
                    // This outermost call is the only level of recursion where assumeUnblocked is empty,
                    // and it is only when assumeUnblocked is empty that we want to update the results array.
                    // So we pass it as a ReadOnlySpan to guarantee this.
                    ReadOnlySpan<BlockedFlag> readOnlyResults = results;
                    bool blocked = IsBlocked(grid, readOnlyResults, assumeUnblocked, loc);
                    results[size.LocIndex(loc)] = blocked ? BlockedFlag.Blocked : BlockedFlag.Unblocked;
                }
            }
        }

        /// <summary>
        /// Determine whether the given <param name="loc"> is blocked.
        /// Some trivial cases are
        /// * a vacant loc is unblocked
        /// * a normal enemy is blocked (it never moves)
        /// The non-trivial case is to assume that the loc is unblocked and recursively check
        /// that all of its dependencies are also unblocked.
        /// </summary>
        private static bool IsBlocked(IReadOnlyGrid grid, ReadOnlySpan<BlockedFlag> results, Span<bool> assumeUnblocked, Loc loc)
        {
            var size = grid.Size;

            if (!grid.InBounds(loc))
            {
                return true; // can't fall off the grid
            }

            var idx = size.LocIndex(loc);
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
            var occDir = occ.Direction;

            if (kind == OccupantKind.None)
            {
                return false;
            }
            else if (kind == OccupantKind.Enemy && occDir == Direction.None)
            {
                return true;
            }
            // an Enemy with Direction=Down indicates a falling enemy
            else if (kind == OccupantKind.Catalyst || (kind == OccupantKind.Enemy && occDir == Direction.Down))
            {
                var temp = assumeUnblocked[idx];
                assumeUnblocked[idx] = true;

                bool isBlocked = false;
                var deps = occ.Direction | Direction.Down;
                foreach (var dir in Lists.Directions.DRLU)
                {
                    if (deps.HasFlag(dir))
                    {
                        isBlocked = isBlocked || IsBlocked(grid, results, assumeUnblocked, loc.Neighbor(dir));
                    }
                }

                assumeUnblocked[idx] = temp;
                return isBlocked;
            }

            throw new InvalidOperationException("unexpected OccupantKind: " + kind);
        }

        static bool ApplyResults(Grid grid, ReadOnlySpan<BlockedFlag> results, Span<int> fallCounter)
        {
            bool any = false;

            // Bottom-to-top is important here so that we can always copy a value from (x, y) to (x, y-1)
            for (int y = 1; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var fromLoc = new Loc(x, y);
                    var fromIndex = grid.Index(fromLoc);
                    var occ = grid.Get(fromLoc);

                    if (CanFall(occ, results[fromIndex]))
                    {
                        var toLoc = fromLoc.Neighbor(Direction.Down);
                        grid.Set(toLoc, occ);
                        grid.Set(fromLoc, Occupant.None);
                        any = true;

                        var toIndex = grid.Index(toLoc);
                        fallCounter[toIndex] = fallCounter[fromIndex] + 1;
                    }
                }
            }

            return any;
        }

        static bool CanFall(Occupant occ, BlockedFlag blocked)
        {
            return occ.Kind != OccupantKind.None && blocked == BlockedFlag.Unblocked;
        }

        static bool TestResults(IReadOnlyGrid grid, ReadOnlySpan<BlockedFlag> results)
        {
            var size = grid.Size;

            for (int y = 1; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var index = size.LocIndex(loc);
                    var occ = grid.Get(loc);
                    if (CanFall(occ, results[index]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
