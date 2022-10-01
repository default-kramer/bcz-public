using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public static class Lists
    {
        public static class Colors
        {
            public static readonly Color[] RYB = new Color[]
            {
                Color.Red, Color.Yellow, Color.Blue,
            };

            public static readonly Color[] RYBBlank = new Color[]
            {
                Color.Red, Color.Yellow, Color.Blue, Color.Blank,
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

        public static readonly IReadOnlyList<SpawnItem> MainDeck;
        public static readonly IReadOnlyList<SpawnItem> BlanklessDeck;

        static Lists()
        {
            var temp = new List<SpawnItem>();
            foreach (var color in Lists.Colors.RYBBlank)
            {
                foreach (var color2 in Lists.Colors.RYB)
                {
                    temp.Add(new SpawnItem(color, color2));
                }
            }
            MainDeck = temp;

            temp = new List<SpawnItem>();
            foreach (var color in Lists.Colors.RYB)
            {
                foreach (var color2 in Lists.Colors.RYB)
                {
                    temp.Add(new SpawnItem(color, color2));
                }
            }
            BlanklessDeck = temp;
        }
    }
}
