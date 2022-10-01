using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// Defines the rotation and x-translation that can be applied to a <see cref="Mover"/>
    /// before dropping it. The values of this struct are somewhat arbitrary; they are
    /// defined by <see cref="Mover.Orientation"/>.
    /// </summary>
    public readonly struct Orientation
    {
        public readonly Direction Direction;
        public readonly int X;

        public Orientation(Direction direction, int x)
        {
            this.Direction = direction;
            this.X = x;
        }
    }

    public readonly struct Move
    {
        public readonly Orientation Orientation;
        public readonly SpawnItem SpawnItem;
        public readonly bool DidBurst;

        public Move(Orientation orientation, SpawnItem spawnItem, bool didBurst)
        {
            this.Orientation = orientation;
            this.SpawnItem = spawnItem;
            this.DidBurst = didBurst;
        }
    }

    public readonly struct Puzzle
    {
        public readonly IImmutableGrid InitialGrid;
        public readonly IReadOnlyList<Move> Moves;
        public readonly Combo Combo;

        public Puzzle(IImmutableGrid initialGrid, IReadOnlyList<Move> moves, Combo combo)
        {
            this.InitialGrid = initialGrid;
            this.Moves = moves;
            this.Combo = combo;
        }

        public ISpawnDeck MakeDeck()
        {
            return new FixedSpawnDeck(Moves.Select(x => x.SpawnItem).ToList());
        }

        class FixedSpawnDeck : ISpawnDeck
        {
            private readonly IReadOnlyList<SpawnItem> queue;
            private int i = 0;

            public FixedSpawnDeck(IReadOnlyList<SpawnItem> queue)
            {
                this.queue = queue;
            }

            public int PeekLimit => queue.Count - i;

            public SpawnItem Peek(int index)
            {
                return queue[i + index];
            }

            public SpawnItem Pop()
            {
                return queue[i++];
            }
        }

        public static IReadOnlyList<Puzzle> FindPuzzles(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
        {
            var b = new PuzzleFinder(ticker, commands);
            b.GO();
            return b.Puzzles;
        }

        class PuzzleFinder
        {
            /// <summary>
            /// Do I need the ticker? Can I get away with State only here??
            /// </summary>
            private readonly Ticker Ticker;

            /// <summary>
            /// It's fine if this list is appended to while the replay is in progress.
            /// (But things might misbehave if you <see cref="Advance(Moment)"/> too far and then
            ///  add a command that happened in the past.)
            /// </summary>
            /// <remarks>
            /// Perhaps a queue would be better?
            /// </remarks>
            private readonly IReadOnlyList<Stamped<Command>> Commands;

            public PuzzleFinder(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
            {
                this.Ticker = ticker;
                this.Commands = commands;
            }

            private IImmutableGrid? comboStartGrid = null;
            private List<Move> comboMoves = new();

            public readonly List<Puzzle> Puzzles = new();

            public void GO()
            {
                Ticker.state.OnComboCompleted += State_OnComboCompleted;
                Ticker.state.OnCatalystSpawned += State_OnCatalystSpawned;

                comboStartGrid = Ticker.state.Grid.MakeImmutable();
                foreach (var c in Commands)
                {
                    if (!Ticker.HandleCommand(c))
                    {
                        throw new Exception("TODO");
                    }
                }
            }

            private void State_OnCatalystSpawned(object? sender, SpawnItem e)
            {
                if (comboStartGrid == null)
                {
                    comboStartGrid = Ticker.state.Grid.MakeImmutable();
                }
                else
                {
                    comboMoves.Add(Ticker.state.PreviousMove);
                }
            }

            private void State_OnComboCompleted(object? sender, Combo combo)
            {
                if (comboStartGrid != null)
                {
                    comboMoves.Add(Ticker.state.PreviousMove);

                    var puzzle = new Puzzle(comboStartGrid, comboMoves.ToList(), combo);
                    Puzzles.Add(puzzle);

                    comboMoves.Clear();
                    comboStartGrid = null;
                }
            }
        }
    }
}
