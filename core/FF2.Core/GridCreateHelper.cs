using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
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
                if (grid.IsVacant(loc))
                {
                    grid.Set(loc, Occupant.MakeEnemy(colorCycle));
                    colorCycle = NextColor(colorCycle);
                    placedCount++;
                }
            }
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
}
