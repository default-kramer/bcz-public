using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    static class GridUtils
    {
        public static bool HasOccupant(this IReadOnlyGridSlim grid, OccupantKind kind, GridWindow window)
        {
            for (int x = window.startX; x < window.endX; x++)
            {
                for (int y = window.startY; y < window.endY; y++)
                {
                    if (grid.Get(new Loc(x, y)).Kind == kind)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
