using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
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

        public Puzzle? Distill()
        {
            Nullable<Puzzle> distilled;
            //distilled = distilled?.RemoveExtraOccupants();
            //distilled = distilled?.ReplaceCatalysts() ?? distilled;
            distilled = this.Probe();
            distilled = distilled?.ShiftDown() ?? distilled;
            distilled = distilled?.RemoveUselessMoves(0);
            return distilled;
        }

        private Puzzle? Try(Grid newGrid)
        {
            var candidate = new Puzzle(newGrid.MakeImmutable(), this.Moves, this.Combo);
            try
            {
                return candidate.Validate() ? candidate : null;
            }
            catch (Exception ex) when (ex.Message.StartsWith("TODO command failed:")) // DO NOT CHECK IN
            {
                return null;
            }
        }

        private Puzzle Probe()
        {
            var clone = Grid.Clone(InitialGrid);
            Puzzle result = this;

            for (int y = 0; y < clone.Height; y++)
            {
                for (int x = 0; x < clone.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var occ = clone.Get(loc);
                    if (occ.Kind != OccupantKind.None && occ.Color != Color.Blank)
                    {
                        var revertInfo = clone.SetWithDivorce(loc, Occupant.IndestructibleEnemy);
                        var temp = Try(clone);
                        if (temp == null)
                        {
                            clone.Revert(revertInfo);
                        }
                        else
                        {
                            result = temp.Value;
                        }
                    }
                }
            }

            for (int y = 0; y < clone.Height; y++)
            {
                for (int x = 0; x < clone.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var occ = clone.Get(loc);
                    if (occ == Occupant.IndestructibleEnemy)
                    {
                        var revertInfo = clone.SetWithDivorce(loc, Occupant.None);
                        var temp = Try(clone);
                        if (temp == null)
                        {
                            clone.Revert(revertInfo);
                        }
                        else
                        {
                            result = temp.Value;
                        }
                    }
                }
            }

            return result;
        }

        private static void ReplaceCatalysts(Grid grid)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var loc = new Loc(x, y);
                    var occ = grid.Get(loc);
                    if (occ.Kind == OccupantKind.Catalyst && occ.Color != Color.Blank)
                    {
                        grid.SetWithDivorce(loc, Occupant.MakeEnemy(occ.Color));
                    }
                }
            }
        }

        public Puzzle? ReplaceCatalysts() // TODO make private
        {
            var clone = Grid.Clone(this.InitialGrid);
            ReplaceCatalysts(clone);
            return Try(clone);
        }

        private Puzzle? RemoveExtraEnemies()
        {
            var resultGrid = RunToCompletion(null)!.State.Grid;

            var clone = Grid.Clone(this.InitialGrid);

            for (int x = 0; x < clone.Width; x++)
            {
                for (int y = 0; y < clone.Height; y++)
                {
                    var loc = new Loc(x, y);
                    var occ = resultGrid.Get(loc);
                    if (occ.Kind == OccupantKind.Enemy)
                    {
                        clone.Set(loc, Occupant.None);
                    }
                }
            }

            return Try(clone);
        }

        public Puzzle? RemoveExtraOccupants()
        {
            var fallTracker = new FallTracker(InitialGrid);
            var resultGrid = RunToCompletion(fallTracker)!.State.Grid;

            var clone = Grid.Clone(this.InitialGrid);
            for (int x = 0; x < clone.Width; x++)
            {
                for (int y = 0; y < clone.Height; y++)
                {
                    var loc = new Loc(x, y);
                    var occ = resultGrid.Get(loc);
                    if (occ.Kind != OccupantKind.None)
                    {
                        var loc2 = fallTracker.GetOriginalLoc(loc);
                        clone.Set(loc2, Occupant.None);
                    }
                }
            }

            // Sometimes we will leave important catalysts hanging in midair, like this:
            // <r r> RR
            //          BB
            // But other times we will leave important catalysts
            //ReplaceCatalysts(clone);

            return Try(clone);
        }

        private Puzzle? ShiftDown()
        {
            var clone = Grid.Clone(this.InitialGrid);
            int count = clone.ShiftToBottom();
            if (count == 0)
            {
                return null;
            }
            return Try(clone);
        }

        private Puzzle RemoveUselessMoves(int index)
        {
            if (index >= Moves.Count)
            {
                return this;
            }

            var newMoves = this.Moves.ToList();
            newMoves.RemoveAt(index);
            var candidate = new Puzzle(this.InitialGrid, newMoves, this.Combo);

            if (candidate.Validate())
            {
                return candidate.RemoveUselessMoves(index);
            }
            else
            {
                return RemoveUselessMoves(index + 1);
            }
        }

        private bool Validate()
        {
            return RunToCompletion(null) != null;
        }

        private PuzzleReplayDriver? RunToCompletion(FallTracker? fallTracker)
        {
            bool ok = false;
            int comboCount = 0;
            var expectedCombo = this.Combo;

            var driver = PuzzleReplayDriver.BuildPuzzleReplay(this);

            driver.State.OnComboCompleted += (s, e) =>
            {
                comboCount++;
                if (e.Equals(expectedCombo))
                {
                    ok = true;
                }
            };

            if (fallTracker != null)
            {
                driver.State.OnFall += (s, e) => fallTracker.Combine(e);
            }

            driver.RunToCompletion();

            return (ok && comboCount == 1) ? driver : null;
        }

        public int TODO_CalculateScore()
        {
            var driver = RunToCompletion(null);
            if (driver == null)
            {
                throw new Exception("TODO need separate data types - (unsolved puzzle and solved puzzle)");
            }
            return driver.State.Score;
        }
    }
}
