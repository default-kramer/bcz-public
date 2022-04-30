using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DeckItem = System.ValueTuple<FF2.Core.Color, FF2.Core.Color>;

namespace FF2.Core
{
    // State.Kind contains the action we are about to attempt.
    public enum StateKind : int
    {
        Spawning = 0,
        Waiting = 1,
        Falling = 2,
        Destroying = 3,
        Unreachable = int.MaxValue,
    }

    public struct M2
    {
        public readonly Loc LocA;
        public readonly Loc LocB;
        public readonly Occupant OccA;
        public readonly Occupant OccB;

        public M2(Loc locA, Occupant occA, Loc locB, Occupant occB)
        {
            this.LocA = locA;
            this.OccA = occA;
            this.LocB = locB;
            this.OccB = occB;
        }

        public bool Translate(int amount, Grid grid, out M2 result)
        {
            var x1 = LocA.X;
            var x2 = LocB.X;
            x1 += amount;
            x2 += amount;
            if (x1 < 0 || x2 < 0 || x1 >= grid.Width || x2 >= grid.Width)
            {
                result = this;
                return false;
            }

            result = new M2(new Loc(x1, LocA.Y), OccA, new Loc(x2, LocB.Y), OccB);
            return true;
        }

        public M2 PreviewPlummet(Grid grid)
        {
            var a = LocA;
            var b = LocB;
            while (a.Y >= 0 && b.Y >= 0 && grid.IsVacant(a) && grid.IsVacant(b))
            {
                a = a.Neighbor(Direction.Down);
                b = b.Neighbor(Direction.Down);
            }

            a = a.Neighbor(Direction.Up);
            b = b.Neighbor(Direction.Up);

            return new M2(a, OccA, b, OccB);
        }

        public Occupant? GetOcc(Loc loc)
        {
            if (loc == LocA) { return OccA; }
            if (loc == LocB) { return OccB; }
            return null;
        }

        public M2? Translate(Direction dir, Grid grid)
        {
            var a = LocA.Neighbor(dir);
            var b = LocB.Neighbor(dir);
            if (grid.InBounds(a) && grid.InBounds(b))
            {
                return new M2(a, OccA, b, OccB);
            }
            return null;
        }
    }

    public sealed class State : IDisposable
    {
        private readonly Grid grid;
        private readonly InfiniteDeck<DeckItem> spawnDeck;

        private M2? mover;

        /// <summary>
        /// Holds the action that will be attempted on the next <see cref="Tick"/>.
        /// </summary>
        public StateKind Kind { get; private set; }

        private State(Grid grid, InfiniteDeck<DeckItem> spawnDeck)
        {
            this.grid = grid;
            this.spawnDeck = spawnDeck;
            mover = null;
            Kind = StateKind.Spawning;
        }

        public IReadOnlyGrid Grid { get { return grid; } }

        private static readonly IReadOnlyList<DeckItem> MainDeck;

        static State()
        {
            var temp = new List<DeckItem>();
            foreach (var color in Lists.Colors.RYB)
            {
                foreach (var color2 in Lists.Colors.RYB)
                {
                    temp.Add(ValueTuple.Create(color, color2));
                }
            }
            MainDeck = temp;
        }

        public static State Create(PRNG prng)
        {
            var deck = new InfiniteDeck<DeckItem>(MainDeck, prng.Clone());
            var grid = Core.Grid.Create(prng);
            return new State(grid, deck);
        }

        private bool Spawn()
        {
            if (Kind != StateKind.Spawning)
            {
                throw new Exception("State got hosed: " + Kind);
            }
            if (mover.HasValue)
            {
                throw new Exception("State got hosed: mover already exists");
            }

            var colors = spawnDeck.Pop();
            var occA = Occupant.MakeCatalyst(colors.Item1, Direction.Right);
            var occB = Occupant.MakeCatalyst(colors.Item2, Direction.Left);
            var locA = new Loc(grid.Width / 2 - 1, grid.Height - 1);
            var locB = locA.Neighbor(Direction.Right);
            mover = new M2(locA, occA, locB, occB);
            return true;
        }

        /// <summary>
        /// Return false if nothing changes, or if <see cref="Kind"/> is the only thing that changes.
        /// Otherwise return true after executing some "significant" change.
        /// </summary>
        public bool Tick()
        {
            switch (Kind)
            {
                case StateKind.Spawning:
                    return ChangeKind(Spawn(), StateKind.Waiting, StateKind.Unreachable);
                case StateKind.Waiting:
                    return false;
                case StateKind.Falling:
                    return ChangeKind(grid.Fall(), StateKind.Falling, StateKind.Destroying);
                case StateKind.Destroying:
                    return ChangeKind(grid.Destroy(), StateKind.Falling, StateKind.Spawning);
                default:
                    throw new Exception("Unexpected kind: " + Kind);
            }
        }

        public M2? PreviewPlummet()
        {
            return mover?.PreviewPlummet(grid);
        }

        public bool Plummet()
        {
            if (Kind != StateKind.Waiting || mover == null)
            {
                return false;
            }

            var m = mover.Value;
            if ("plummet".ToString() == "nope")
            {
                m = m.PreviewPlummet(grid);
            }
            grid.Set(m.LocA, m.OccA);
            grid.Set(m.LocB, m.OccB);

            mover = null;
            Kind = StateKind.Falling;
            return true;
        }

        private bool ChangeKind(bool significantChange, StateKind a, StateKind b)
        {
            Kind = significantChange ? a : b;
            return significantChange;
        }

        public void Dispose()
        {
            grid.Dispose();
            spawnDeck.Dispose();
        }

        public bool Move(Direction dir)
        {
            if (!mover.HasValue)
            {
                return false;
            }
            var m = mover.Value;

            switch (dir)
            {
                case Direction.Left:
                case Direction.Right:
                    var translated = m.Translate(dir, grid);
                    if (translated.HasValue)
                    {
                        mover = translated.Value;
                        return true;
                    }
                    return false;
            }

            return false;
        }
    }
}
