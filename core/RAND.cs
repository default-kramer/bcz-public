using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public static class RAND
    {
        private static readonly Random rand = new Random();

        public static Grid RandomGrid()
        {
            var grid = Grid.Create(8, 20);
            foreach (var color in Lists.Colors.RYB)
            {
                for (int i = 0; i < 20; i++)
                {
                    var x = rand.Next(grid.Width);
                    var y = rand.Next(grid.Height - 5);
                    var loc = new Loc(x, y);

                    var idx = rand.Next(Lists.Directions.DRLU0.Length);
                    var dir = Lists.Directions.DRLU0[idx];
                    if (x == 0 && dir == Direction.Left)
                    {
                        dir = Direction.None;
                    }
                    if (x == grid.Width - 1 && dir == Direction.Right)
                    {
                        dir = Direction.None;
                    }

                    grid.Set(loc, new Occupant(OccupantKind.Catalyst, color, dir));
                }
            }
            return grid;
        }
    }
}
