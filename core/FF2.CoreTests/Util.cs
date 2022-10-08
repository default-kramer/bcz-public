using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;

namespace FF2.CoreTests
{
    static class Util
    {
        public static IEnumerable<(Loc, Occupant)> Iterate(this IReadOnlyGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    yield return (loc, grid.Get(loc));
                }
            }
        }

        public static int Count(this IReadOnlyGrid grid, OccupantKind kind)
        {
            return grid.Iterate().Where(x => x.Item2.Kind == kind).Count();
        }

        public static int CountEnemies(this IReadOnlyGrid grid)
        {
            return grid.Iterate()
                .Where(x => x.Item2.Kind == OccupantKind.Enemy && x.Item2.Color != Color.Blank)
                .Count();
        }
    }
}
