using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public class ComboDistillery
    {
        // Input - a replay
        // Output - a sequence of distilled combos
        // * Starting Grid
        // * Catalyst Queue
        // * Commands
        // * Ending Grid
        // * The Combo object
        public readonly struct Puzzle
        {
            public readonly IImmutableGrid InitialGrid;
            public readonly IReadOnlyList<SpawnItem> Queue;
            // TODO - Instead of commands, it would be better to capture something
            // like (SpawnItem, Orientation, DidBurst?) where Orientation is
            // something like (Rotation, Translation)
            public readonly IReadOnlyList<Stamped<Command>> Commands;
            public readonly Combo Combo;

            public Puzzle(IImmutableGrid initialGrid, IReadOnlyList<SpawnItem> queue, IReadOnlyList<Stamped<Command>> commands, Combo combo)
            {
                this.InitialGrid = initialGrid;
                this.Queue = queue;
                this.Commands = commands;
                this.Combo = combo;
            }

            public ISpawnDeck MakeDeck()
            {
                return new FixedSpawnDeck(Queue);
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
        }

        public static IReadOnlyList<Puzzle> FindPuzzles(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
        {
            var b = new BLAH(ticker, commands);
            b.GO();
            return b.Puzzles;
        }

        class BLAH
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

            public BLAH(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
            {
                this.Ticker = ticker;
                this.Commands = commands;
            }

            private IImmutableGrid? comboStartGrid = null;
            private List<SpawnItem> comboQueue = new();
            private List<Stamped<Command>> comboCommands = new();

            public readonly List<Puzzle> Puzzles = new();

            public void GO()
            {
                Ticker.state.OnComboCompleted += State_OnComboCompleted;
                Ticker.state.OnCatalystSpawned += State_OnCatalystSpawned;

                comboStartGrid = Ticker.state.Grid.MakeImmutable();
                foreach (var c in Commands)
                {
                    comboCommands.Add(c);
                    if (!Ticker.HandleCommand(c))
                    {
                        throw new Exception("TODO");
                    }
                }
            }

            private void State_OnCatalystSpawned(object? sender, SpawnItem e)
            {
                comboStartGrid = comboStartGrid ?? Ticker.state.Grid.MakeImmutable();
                comboQueue.Add(e);
            }

            private void State_OnComboCompleted(object? sender, Combo combo)
            {
                if (comboStartGrid != null)
                {
                    // The last command actually belongs to the next puzzle.
                    // I'm not sure why right now... A bug in the state/ticker maybe?
                    var commands = comboCommands.Take(comboCommands.Count - 1).ToList();
                    var lastCommand = comboCommands.Last();

                    var puzzle = new Puzzle(comboStartGrid, comboQueue.ToList(), commands, combo);
                    Puzzles.Add(puzzle);

                    comboQueue.Clear();
                    comboCommands.Clear();
                    comboCommands.Add(lastCommand);
                    comboStartGrid = null;
                }
            }
        }
    }
}
