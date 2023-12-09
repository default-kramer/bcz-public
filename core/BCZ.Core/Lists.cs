using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
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

        public static readonly int[] DummyBuffer = new int[1024];

        /// <summary>
        /// Useful for splitting strings.
        /// </summary>
        public static readonly string[] Newlines = new[] { "\r\n", "\n" };

        /// <summary>
        /// Contains all possible orderings of the list (Red, Yellow, Blue, Blank).
        /// Used for wide layout grid setup.
        /// </summary>
        public static readonly IReadOnlyList<IReadOnlyList<Color>> WideLayoutRowPatterns = BuildPermutations(Color.Red, Color.Yellow, Color.Blue, Color.Blank);

        private static IReadOnlyList<IReadOnlyList<T>> BuildPermutations<T>(params T[] items)
        {
            // This is not a very efficient implementation, but it's good enough for startup-only code on small data sets.
            var initial = new List<T>() { items.First() };
            var perms = new List<List<T>>() { initial };

            for (int i = 1; i < items.Length; i++)
            {
                perms = AddToPermutations(perms, items[i]).ToList();
            }

            return perms;
        }

        private static IEnumerable<List<T>> AddToPermutations<T>(IEnumerable<List<T>> perms, T newItem)
        {
            foreach (var perm in perms)
            {
                for (int i = 0; i <= perm.Count; i++)
                {
                    var clone = new List<T>(perm);
                    clone.Insert(i, newItem);
                    yield return clone;
                }
            }
        }

        static Lists()
        {
            var temp = new List<SpawnItem>();
            foreach (var color in Lists.Colors.RYBBlank)
            {
                foreach (var color2 in Lists.Colors.RYB)
                {
                    temp.Add(SpawnItem.MakeCatalystPair(color, color2));
                }
            }
            MainDeck = temp;

            temp = new List<SpawnItem>();
            foreach (var color in Lists.Colors.RYB)
            {
                foreach (var color2 in Lists.Colors.RYB)
                {
                    temp.Add(SpawnItem.MakeCatalystPair(color, color2));
                }
            }
            BlanklessDeck = temp;
        }
    }
}
