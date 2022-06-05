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
        Empty = 0,
        Spawning = 1,
        Waiting = 2,
        Falling = 3,
        Destroying = 4,
        Unreachable = int.MaxValue,
    }

    // Does timing-related stuff belong in the state class? Or at a higher level?
    // Meh, I don't know, so just start hacking and we can always refactor later
    public sealed class State : IDisposable
    {
        private readonly Grid grid;
        private readonly InfiniteDeck<DeckItem> spawnDeck;
        private Mover? mover;
        private CorruptionManager corruption;
        private Combo currentCombo;
        private readonly PenaltyManager penalties;
        private PenaltySchedule penaltySchedule;
        private int waitingMillis = 0;

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
            corruption = new CorruptionManager();
            currentCombo = Combo.Empty;
            penalties = new PenaltyManager();
            penaltySchedule = PenaltySchedule.BasicSchedule(10 * 1000);
        }

        public IReadOnlyGrid Grid { get { return grid; } }

        private static readonly IReadOnlyList<DeckItem> MainDeck;

        static State()
        {
            var temp = new List<DeckItem>();
            foreach (var color in Lists.Colors.RYBBlank)
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

        public void Elapse(int millis)
        {
            if (Kind == StateKind.Waiting)
            {
                waitingMillis += millis;

                corruption = corruption.Elapse(millis);

                if (penaltySchedule.TryAdvance(waitingMillis, out var nextPS))
                {
                    penalties.Add(penaltySchedule.Penalty);
                    penaltySchedule = nextPS;

                    corruption = corruption.OnPenaltiesChanged(penalties);
                }
            }
        }

        public decimal CorruptionProgress { get { return corruption.Progress; } }

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
            var locA = new Loc(grid.Width / 2 - 1, 0);
            var locB = locA.Neighbor(Direction.Right);
            mover = new Mover(locA, occA, locB, occB);
            return true;
        }

        private bool Destroy(TickCalculations calculations)
        {
            var result = grid.Destroy(calculations);
            if (result)
            {
                currentCombo = currentCombo.AfterDestruction(calculations);
            }
            else
            {
                corruption = corruption.OnComboCompleted(currentCombo);
                currentCombo = Combo.Empty;
            }
            return result;
        }

        /// <summary>
        /// Return false if nothing changes, or if <see cref="Kind"/> is the only thing that changes.
        /// Otherwise return true after executing some "significant" change.
        /// </summary>
        public bool Tick(TickCalculations calculations)
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
                    return ChangeKind(Destroy(calculations), StateKind.Falling, StateKind.Spawning);
                default:
                    throw new Exception("Unexpected kind: " + Kind);
            }
        }

        public Mover? PreviewPlummet()
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
            if (true)//"plummet".ToString() == "nope")
            {
                m = m.PreviewPlummet(grid);
            }
            else
            {
                m = m.ToTop(grid.Height);
            }
            grid.Set(m.LocA, m.OccA);
            grid.Set(m.LocB, m.OccB);

            mover = null;
            Kind = StateKind.Falling;
            return true;
        }

        public bool Burst()
        {
            if (!Plummet())
            {
                return false;
            }

            grid.Burst();
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
                    var translated = m.Translate(dir, grid.Width);
                    if (translated.HasValue)
                    {
                        mover = translated.Value;
                        return true;
                    }
                    return false;
            }

            return false;
        }

        public bool Rotate(bool clockwise)
        {
            if (!mover.HasValue)
            {
                return false;
            }
            mover = mover.Value.Rotate(clockwise, grid.Width);
            return true;
        }
    }
}
