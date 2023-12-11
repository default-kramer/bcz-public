using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
