using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    static class WideLayoutHelper
    {
        private static class Windows
        {
            // ASSUMPTION - wide layout will always be a 16x16 grid
            public const int ExpectedWidth = 16;
            public const int ExpectedHeight = 16;

            public static readonly GridWindow LeftFull = new GridWindow(0, 8, 0, 16);
            public static readonly GridWindow RightFull = new GridWindow(8, 16, 0, 16);
            public static readonly GridWindow LeftEnemy = new GridWindow(3, 7, 0, 4);
            public static readonly GridWindow RightEnemy = new GridWindow(9, 13, 0, 4);
        }

        public static bool MaybeRefillWideLayout(Grid grid, PRNG prng)
        {
            if (grid.Width != Windows.ExpectedWidth || grid.Height != Windows.ExpectedHeight)
            {
                throw new Exception($"Unexpected grid size for wide layout: {grid.Width} / {grid.Height}");
            }

            // Make sure we refill both (don't short-circuit if left returns true)
            bool left = MaybeRefillWideLayout(prng, grid, Windows.LeftEnemy, Windows.LeftFull);
            bool right = MaybeRefillWideLayout(prng, grid, Windows.RightEnemy, Windows.RightFull);
            return left || right;
        }

        private static bool MaybeRefillWideLayout(PRNG prng, Grid grid, GridWindow enemyWindow, GridWindow fullWindow)
        {
            if (HasEnemy(grid, enemyWindow))
            {
                return false; // no refill needed
            }

            ClearGrid(grid, fullWindow);
            AddEnemies(prng, grid, enemyWindow);
            return true;
        }

        private static bool HasEnemy(IReadOnlyGrid grid, GridWindow window)
        {
            for (int x = window.startX; x < window.endX; x++)
            {
                for (int y = window.startY; y < window.endY; y++)
                {
                    if (grid.Get(new Loc(x, y)).Kind == OccupantKind.Enemy)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddEnemies(PRNG prng, Grid grid, GridWindow window)
        {
            int listCount = Lists.WideLayoutRowPatterns.Count;
            int width = window.endX - window.startX;

            for (int y = window.startY; y < window.endY; y++)
            {
                // If we choose a pattern that makes a group of 3, we will redo the current row.
                bool redo = true;
                while (redo)
                {
                    redo = false;

                    // Choose a random pattern of (Red, Yellow, Blue, Blank)
                    IReadOnlyList<Color> pattern = Lists.WideLayoutRowPatterns[prng.NextInt32(listCount)];
                    for (int x = 0; !redo && x < width; x++)
                    {
                        var loc = new Loc(window.startX + x, y);

                        var color = pattern[x];
                        if (color == Color.Blank)
                        {
                            grid.Set(loc, Occupant.None);
                        }
                        else
                        {
                            grid.Set(loc, Occupant.MakeEnemy(color));
                            redo = grid.CountColorMatches(loc, Direction.Down) >= 3;
                        }
                    }
                }
            }
        }

        private static void ClearGrid(Grid grid, GridWindow window)
        {
            for (int x = window.startX; x < window.endX; x++)
            {
                for (int y = window.startY; y < window.endY; y++)
                {
                    grid.SetWithDivorce(new Loc(x, y), Occupant.None);
                }
            }
        }
    }
}
