using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    static class Lists
    {
        public static class Colors
        {
            public static readonly Color[] RYB = new Color[]
            {
                Color.Red, Color.Yellow, Color.Blue,
            };
        }

        public static class Directions
        {
            public static readonly Direction[] DRLU = new Direction[] {
                Direction.Down,
                Direction.Right,
                Direction.Left,
                Direction.Up,
            };

            public static readonly Direction[] DRLU0 = new Direction[] {
                Direction.Down,
                Direction.Right,
                Direction.Left,
                Direction.Up,
                Direction.None,
            };
        }
    }
}
