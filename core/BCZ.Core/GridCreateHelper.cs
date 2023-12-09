using System;
using System.Collections.Generic;

namespace BCZ.Core
{
    readonly struct GridWindow
    {
        public readonly int startX;
        public readonly int endX;
        public readonly int startY;
        public readonly int endY;

        public GridWindow(int startX, int endX, int startY, int endY)
        {
            this.startX = startX;
            this.endX = endX;
            this.startY = startY;
            this.endY = endY;
        }

        public static GridWindow MakeFullWindow(IReadOnlyGridSlim grid) => new GridWindow(0, grid.Size.Width, 0, grid.Size.Height);

        public bool InBounds(Loc loc)
        {
            return loc.X >= startX
                && loc.X < endX
                && loc.Y >= startY
                && loc.Y < endY;
        }
    }

    static class GridCreateHelper
    {
        public static void SetupHardcodedGrid(Grid grid)
        {
            const int minY = 8;
            grid.Set(new Loc(0, minY), Occupant.MakeCatalyst(Color.Blue, Direction.Right));
            grid.Set(new Loc(1, minY), Occupant.MakeCatalyst(Color.Red, Direction.Left));
            grid.Set(new Loc(1, minY + 4), Occupant.MakeCatalyst(Color.Yellow, Direction.Right));
            grid.Set(new Loc(2, minY + 4), Occupant.MakeCatalyst(Color.Blue, Direction.Left));
            grid.Set(new Loc(6, minY + 6), Occupant.MakeCatalyst(Color.Yellow, Direction.Up));
            grid.Set(new Loc(6, minY + 7), Occupant.MakeCatalyst(Color.Red, Direction.Down));
            grid.Set(new Loc(3, minY + 3), Occupant.MakeEnemy(Color.Red));
            grid.Set(new Loc(3, minY + 5), Occupant.MakeEnemy(Color.Yellow));

            // Test a falling enemy. If we support this, we would need to add code
            // to change falling enemies back to regular enemies after they land.
            grid.Set(new Loc(3, minY + 2), new Occupant(OccupantKind.Enemy, Color.Blue, Direction.Down));
        }

        public static void SetupSimpleGrid(Grid grid, PRNG rand)
        {
            SetupGrid(grid, rand, 15, 5, 2);
        }

        public static void SetupGrid(Grid grid, PRNG prng, ISinglePlayerSettings settings)
        {
            SetupGrid(grid, prng, settings.EnemyCount, settings.EnemiesPerStripe, settings.RowsPerStripe);
        }

        public static void SetupGrid(Grid grid, PRNG prng, int totalEnemies, int enemiesPerStripe, int rowsPerStripe)
        {
            if (totalEnemies < 1 || enemiesPerStripe < 1 || rowsPerStripe < 1)
            {
                throw new ArgumentException($"bad args: {totalEnemies}, {enemiesPerStripe}, {rowsPerStripe}");
            }

            var colorCycle = Lists.Colors.RYB[prng.NextInt32(3)];
            int yLo = 0;
            int remainingEnemies = totalEnemies;
            while (remainingEnemies > 0)
            {
                int enemiesThisStripe = Math.Min(enemiesPerStripe, remainingEnemies);
                int yHi = yLo + rowsPerStripe;
                if (yHi >= grid.Height)
                {
                    throw new ArgumentException($"Invalid enemy batching vs. grid height: {grid.Height}, {totalEnemies}, {enemiesPerStripe}, {rowsPerStripe}");
                }
                PlaceEnemiesInRows(grid, prng, yLo, yHi, enemiesThisStripe, ref colorCycle);
                remainingEnemies -= enemiesThisStripe;
                yLo = yHi;
            }
        }

        private static void PlaceEnemiesInRows(Grid grid, PRNG rand, int yLo, int yHi, int enemyCount, ref Color colorCycle)
        {
            int width = grid.Width;
            int height = yHi - yLo;
            int size = width * height;

            if (enemyCount > size)
            {
                throw new ArgumentException($"Too many enemies requested: {enemyCount}, {grid.Width}, {yLo}, {yHi}");
            }

            int placedCount = 0;
            while (placedCount < enemyCount)
            {
                int x = rand.NextInt32(width);
                int y = rand.NextInt32(height) + yLo;
                var loc = new Loc(x, y);
                if (grid.IsVacant(loc) && !WouldCreateGroupOf3(grid, (loc, colorCycle)))
                {
                    grid.Set(loc, Occupant.MakeEnemy(colorCycle));
                    colorCycle = NextColor(colorCycle);
                    placedCount++;
                }
            }
        }

        private static bool WouldCreateGroupOf3(IReadOnlyGrid grid, (Loc loc, Color color) proposed)
        {
            int matches = 0;
            for (int dx = -2; dx <= 2; dx++)
            {
                var iterLoc = proposed.loc.Add(dx, 0);
                if (Match3Helper(ref matches, grid, proposed, iterLoc))
                {
                    return true;
                }
            }

            matches = 0;
            for (int dy = -2; dy <= 2; dy++)
            {
                var iterLoc = proposed.loc.Add(0, dy);
                if (Match3Helper(ref matches, grid, proposed, iterLoc))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Match3Helper(ref int matches, IReadOnlyGrid grid, (Loc loc, Color color) proposed, Loc iterLoc)
        {
            if (!grid.InBounds(iterLoc))
            {
                matches = 0;
            }
            else if (iterLoc == proposed.loc || grid.Get(iterLoc).Color == proposed.color)
            {
                matches++;
                if (matches == 3)
                {
                    return true;
                }
            }
            else
            {
                matches = 0;
            }
            return false;
        }

        private static Color NextColor(Color color)
        {
            return color switch
            {
                Color.Red => Color.Yellow,
                Color.Yellow => Color.Blue,
                Color.Blue => Color.Red,
                _ => color,
            };
        }
    }

    static class WideLayoutHelper
    {
        // ASSUMPTION - wide layout will always be a 16x16 grid
        private static class Windows
        {
            public static readonly GridWindow LeftFull = new GridWindow(0, 8, 0, 16);
            public static readonly GridWindow RightFull = new GridWindow(8, 16, 0, 16);
            public static readonly GridWindow LeftEnemy = new GridWindow(3, 7, 0, 4);
            public static readonly GridWindow RightEnemy = new GridWindow(9, 13, 0, 4);
        }

        public static bool MaybeRefillWideLayout(Grid grid, PRNG prng)
        {
            if (grid.Width != 16 || grid.Height != 16)
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
