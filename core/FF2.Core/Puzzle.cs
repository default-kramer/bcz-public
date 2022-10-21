using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public abstract class PuzzleBase
    {
        private readonly Lazy<Puzzle?> solution;

        public PuzzleBase()
        {
            solution = new Lazy<Puzzle?>(__Solve);
        }

        protected abstract Puzzle? __Solve(); // Wait this is dumb because Puzzle : UnsolvePuzzle anyway...

        internal Puzzle? Solve(int minAGC)
        {
            var result = solution.Value;
            if (result != null && result.LastCombo.AdjustedGroupCount >= minAGC)
            {
                return result;
            }
            return null;
        }
    }

    public class Puzzle : UnsolvedPuzzle
    {
        public readonly int ExpectedScore;
        public readonly Combo LastCombo;
        public readonly IImmutableGrid ResultGrid;

        public Puzzle(UnsolvedPuzzle d, int score, Combo lastCombo, IImmutableGrid resultGrid) : base(d)
        {
            this.ExpectedScore = score;
            this.LastCombo = lastCombo;
            this.ResultGrid = resultGrid;
        }

        protected override Puzzle? __Solve()
        {
            return this;
        }

        public Puzzle FinalAdjustment()
        {
            var result = this.RemoveUselessMoves(0);
            result = result.StabilizeBottom();
            return result;
        }

        private Puzzle RemoveUselessMoves(int index)
        {
            if (index >= Moves.Count)
            {
                return this;
            }

            var newMoves = this.Moves.ToList();
            newMoves.RemoveAt(index);
            var candidate = new UnsolvedPuzzle(this.InitialGrid, newMoves, this.OriginalCombo);
            var result = candidate.Solve(this.OriginalCombo.AdjustedGroupCount);
            return result?.RemoveUselessMoves(index)
                ?? this.RemoveUselessMoves(index + 1);
        }

        private Puzzle StabilizeBottom()
        {
            var grid = Grid.Clone(this.InitialGrid);
            if (grid.Fall(Lists.DummyBuffer))
            {
                return StabilizeBottom(0, Grid.Clone(this.InitialGrid));
            }
            else
            {
                return this;
            }
        }

        private Puzzle StabilizeBottom(int x, Grid grid)
        {
            if (x >= grid.Width)
            {
                return this;
            }

            for (int y = 0; y < grid.Height; y++)
            {
                var loc = new Loc(x, y);
                var occ = grid.Get(loc);
                if (occ.Kind == OccupantKind.Enemy)
                {
                    return StabilizeBottom(x + 1, grid);
                }
                else if (occ.Kind == OccupantKind.Catalyst)
                {
                    var revertInfo = grid.SetWithDivorce(loc, Occupant.MakeEnemy(occ.Color));
                    var result = Try(grid);
                    if (result != null)
                    {
                        return result.StabilizeBottom(x + 1, grid);
                    }
                    else
                    {
                        grid.Revert(revertInfo);
                        return this.StabilizeBottom(x + 1, grid);
                    }
                }
            }

            return this.StabilizeBottom(x + 1, grid);
        }

#if DEBUG
        // TODO clean this up!!!!
        public bool CheckString(string expected)
        {
            var lines = expected.Split(Lists.Newlines, StringSplitOptions.RemoveEmptyEntries);
            var moves = lines.TakeWhile(x => !x.StartsWith("==")).ToArray();
            var grid = lines.Skip(moves.Length + 1).ToArray();
            bool gridOk = InitialGrid.DiffGridString(grid) == "ok";
            return gridOk && CheckMoves(moves);
        }

        public bool CheckMoves(params string[] expectedMoves)
        {
            // Use a grid of height 2 to reuse the CheckGridString logic.
            // We will clear this grid, drop the move, and check that it matches.

            var tempGrid = Grid.Create(InitialGrid.Width, 2); // height of 2 is enough for horizontal or vertical
            string emptyRow = new string(' ', tempGrid.Width * 3); // 3 chars per cell
            if (tempGrid.DiffGridString(emptyRow, emptyRow) != "ok")
            {
                throw new Exception("Assert failed");
            }

            // consume expectedMoves from bottom to top
            using var iter = expectedMoves.Reverse().GetEnumerator();

            foreach (var move in Moves)
            {
                tempGrid.Clear();

                if (!tempGrid.Place(move))
                {
                    throw new Exception("WTF");
                }
                var dir = move.Orientation.Direction;

                string expected1 = emptyRow;
                string expected2 = iter.Advance();
                if (dir == Direction.Up || dir == Direction.Down)
                {
                    expected1 = iter.Advance();
                }

                if (tempGrid.DiffGridString(expected1, expected2) != "ok")
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }

    public class UnsolvedPuzzle : PuzzleBase
    {
        public readonly IImmutableGrid InitialGrid;
        public readonly IReadOnlyList<Move> Moves;
        public readonly Combo OriginalCombo;

        public UnsolvedPuzzle(UnsolvedPuzzle def) : this(def.InitialGrid, def.Moves, def.OriginalCombo) { }

        public UnsolvedPuzzle(IImmutableGrid initialGrid, IReadOnlyList<Move> moves, Combo combo)
        {
            this.InitialGrid = initialGrid;
            this.Moves = moves;
            this.OriginalCombo = combo;
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

        public static IReadOnlyList<UnsolvedPuzzle> FindRawPuzzles(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
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

            public readonly List<UnsolvedPuzzle> Puzzles = new();

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

                    var puzzle = new UnsolvedPuzzle(comboStartGrid, comboMoves.ToList(), combo);
                    Puzzles.Add(puzzle);

                    comboMoves.Clear();
                    comboStartGrid = null;
                }
            }
        }

        public Puzzle? Distill()
        {
            UnsolvedPuzzle? distilled;
            //distilled = distilled?.RemoveExtraOccupants();
            //distilled = distilled?.ReplaceCatalysts() ?? distilled;
            distilled = this.Probe();
            distilled = distilled?.ShiftDown() ?? distilled;
            var p = distilled?.Solve(this.OriginalCombo.AdjustedGroupCount);
            return p?.FinalAdjustment();
        }

        protected Puzzle? Try(Grid newGrid)
        {
            var candidate = new UnsolvedPuzzle(newGrid.MakeImmutable(), this.Moves, this.OriginalCombo);
            try
            {
                return candidate.Solve(this.OriginalCombo.AdjustedGroupCount); // TODO unclear
            }
            catch (Exception ex) when (ex.Message.StartsWith("TODO command failed:")) // DO NOT CHECK IN
            {
                return null;
            }
        }

        private UnsolvedPuzzle Probe()
        {
            var clone = Grid.Clone(InitialGrid);
            UnsolvedPuzzle result = this;

            for (int y = 0; y < clone.Height; y++)
            {
                for (int x = 0; x < clone.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var occ = clone.Get(loc);
                    // TODO fix the test here:
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
                            result = temp;
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
                            result = temp;
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

        public UnsolvedPuzzle? ReplaceCatalysts() // TODO make private
        {
            var clone = Grid.Clone(this.InitialGrid);
            ReplaceCatalysts(clone);
            return Try(clone);
        }

        private UnsolvedPuzzle? RemoveExtraEnemies()
        {
            var resultGrid = Solve(OriginalCombo.AdjustedGroupCount)!.ResultGrid;

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

        [Obsolete("TODO do we need this?")]
        public UnsolvedPuzzle? RemoveExtraOccupantsTODO()
        {
            var fallTracker = new FallTracker(InitialGrid);
            var result = SolveAgain(fallTracker.ResetFallCountBuffer());
            if (result == null)
            {
                return null;
            }
            var resultGrid = result.ResultGrid;

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

        private UnsolvedPuzzle? ShiftDown()
        {
            var clone = Grid.Clone(this.InitialGrid);
            int count = clone.ShiftToBottom();
            if (count == 0)
            {
                return null;
            }
            return Try(clone);
        }

        protected override Puzzle? __Solve()
        {
            return SolveAgain(Lists.DummyBuffer);
        }

        // TODO the fallCountBuffer will not be accurate if there is more than one fall cycle.
        // In general, we probably want to abort immediately if there is more than one fall cycle.
        private Puzzle? SolveAgain(Span<int> fallCountBuffer)
        {
            var grid = Grid.Clone(InitialGrid);
            var width = grid.Width;
            var height = grid.Height;
            var combo = Combo.Empty;
            var tc = new TickCalculations(grid);
            int score = 0;
            var p = PayoutTable.DefaultScorePayoutTable; // TODO make this clear

            foreach (var move in Moves)
            {
                if (!grid.Place(move))
                {
                    return null;
                }
                if (move.DidBurst)
                {
                    grid.Burst();
                }
                grid.FallCompletely(fallCountBuffer);

                tc.Reset();
                combo = Combo.Empty;
                while (grid.Destroy(tc))
                {
                    combo = combo.AfterDestruction(tc);
                    tc.Reset();
                    grid.FallCompletely(fallCountBuffer);
                }
                score += p.GetPayout(combo.AdjustedGroupCount);
            }

            return new Puzzle(this, score, combo, grid.MakeImmutable());
        }
    }
}
