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

        public static bool Fall(Grid grid, Span<BlockedFlag> results, Span<bool> assumeUnblocked)
        {
            results.Fill(BlockedFlag.Unsure);
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
                    // IsBlocked(...) is a recursive function.
                    // This outermost call is the only level of recursion where assumeUnblocked is empty,
                    // and it is only when assumeUnblocked is empty that we want to update the results array.
                    // So we pass it as a ReadOnlySpan to guarantee this.
                    ReadOnlySpan<BlockedFlag> readOnlyResults = results;
                    bool blocked = IsBlocked(grid, readOnlyResults, assumeUnblocked, loc);
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
        /// that all of its dependencies are also unblocked.
        /// </summary>
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
