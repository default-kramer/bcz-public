using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// When you start from the <see cref="InitialGrid"/> and apply the <see cref="Moves"/>
    /// in order, the following guarantees should hold:
    /// 1) Each move will be legal.
    /// 2) The last move will cause destruction and produce the <see cref="LastCombo"/>.
    /// 3) None of the other moves will cause destruction.
    /// </summary>
    public class Puzzle
    {
        public readonly IImmutableGrid InitialGrid;
        public readonly IReadOnlyList<Move> Moves;
        public readonly ComboInfo LastCombo;

        private int StrictCount => this.LastCombo.StrictCombo.AdjustedGroupCount;

        private bool IsNotWorseThan(Puzzle other)
        {
            return StrictCount >= other.StrictCount;
        }

        private bool IsBetterThan(Puzzle? other)
        {
            return other == null || StrictCount > other.StrictCount;
        }

        public IImmutableGrid TEMP_ResultGrid()
        {
            return SolveAndReturnGrid(this.InitialGrid, this.Moves)!.Value.Item1.MakeImmutable();
        }

        public Puzzle(IImmutableGrid initialGrid, IReadOnlyList<Move> moves, ComboInfo lastCombo)
        {
            this.InitialGrid = initialGrid;
            this.Moves = moves;
            this.LastCombo = lastCombo;
        }

        private Puzzle RemoveUselessMoves(int index)
        {
            if (index >= Moves.Count || Moves.Count < 2)
            {
                return this;
            }

            var newMoves = this.Moves.ToList();
            newMoves.RemoveAt(index);

            if (index == newMoves.Count)
            {
                // Make sure the last move is a burst.
                // Perhaps we should only do this if the last move of the original was a burst, but I cannot
                // imagine a good puzzle that is ruined when the last move is changed from non-burst to burst.
                // So I think it's always "safe" to end with a burst.
                newMoves[index - 1] = newMoves[index - 1].MakeBurst();
            }

            var result = Solve(this.InitialGrid, newMoves);
            if (this.IsBetterThan(result))
            {
                return this.RemoveUselessMoves(index + 1);
            }
            else
            {
                return result!.RemoveUselessMoves(index);
            }
        }

        public Puzzle Distill()
        {
            var winner = this;
            var grid = Grid.Clone(this.InitialGrid);

            // It's important to maximize the strict combo before doing anything else,
            // because later steps attempt to remove everything that is not needed to keep
            // the strict combo from regressing.
            ImproveStrictCombo(grid, ref winner);

            Calcify(grid, ref winner);

            Prune(grid, ref winner);

            ShiftDown(grid, ref winner);

            // TODO everything we've done might have left floating occupants.
            // This fact is (usually / always ?) irrelevant because they fall
            // when the first move is played.
            // WARNING! Grid is not reverted here.
            if (grid.FallCompletely(Lists.DummyBuffer)
                && Try(grid, winner, out var candidate)
                && candidate.IsNotWorseThan(winner))
            {
                winner = candidate;
            }

            return winner.RemoveUselessMoves(0);
        }

        /// <summary>
        /// Identify semi-irrelevant occupants by changing them to an <see cref="Occupant.IndestructibleEnemy"/>.
        /// This is less disruptive to the grid's stability than removing them entirely.
        /// </summary>
        private static void Calcify(Grid grid, ref Puzzle winner)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var existing = grid.Get(loc);
                    if (existing.Kind == OccupantKind.None)
                    {
                        continue;
                    }

                    var revertInfo = grid.SetWithDivorce(loc, Occupant.IndestructibleEnemy);
                    if (Try(grid, winner, out var candidate)
                       && candidate.IsNotWorseThan(winner))
                    {
                        winner = candidate;
                    }
                    else
                    {
                        grid.Revert(revertInfo);
                    }
                }
            }
        }

        /// <summary>
        /// The original combo may have permissive (all-catalyst) groups, but we prefer strict groups.
        /// So this tries all possible catalyst->enemy conversions and keeps only those which improve the strict combo.
        /// </summary>
        private static void ImproveStrictCombo(Grid grid, ref Puzzle winner)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var existing = grid.Get(loc);
                    if (existing.Kind != OccupantKind.Catalyst)
                    {
                        continue;
                    }

                    var enemy = Occupant.MakeEnemy(existing.Color);

                    var revertInfo = grid.SetWithDivorce(loc, enemy);
                    if (Try(grid, winner, out var candidate)
                        // Must improve the strict count, otherwise we'll leave it as a catalyst
                        && candidate.IsBetterThan(winner))
                    {
                        winner = candidate;
                    }
                    else
                    {
                        grid.Revert(revertInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Identify totally-irrelevant occupants by removing them from the grid.
        /// </summary>
        private static void Prune(Grid grid, ref Puzzle winner)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var loc = new Loc(x, y);
                    var existing = grid.Get(loc);
                    if (existing.Kind == OccupantKind.None)
                    {
                        continue;
                    }

                    var revertInfo = grid.SetWithDivorce(loc, Occupant.None);
                    if (Try(grid, winner, out var candidate)
                        && candidate.IsNotWorseThan(winner))
                    {
                        winner = candidate;
                    }
                    else
                    {
                        grid.Revert(revertInfo);
                    }
                }
            }
        }

        private static bool Try(IReadOnlyGrid newGrid, Puzzle winner, out Puzzle result)
        {
            return Try(newGrid.MakeImmutable(), winner.Moves, out result);
        }

        private static bool Try(IImmutableGrid newGrid, IReadOnlyList<Move> moves, out Puzzle result)
        {
            var temp = Solve(newGrid, moves);
            if (temp != null)
            {
                result = temp;
                return true;
            }
            result = null!;
            return false;
        }

        private static bool ShiftDown(Grid grid, ref Puzzle winner)
        {
            int shiftAmount = grid.CountEmptyBottomRows();
            while (shiftAmount > 0)
            {
                grid.ShiftDown(shiftAmount);

                if (Try(grid, winner, out var candidate)
                    && candidate.IsNotWorseThan(winner))
                {
                    winner = candidate;
                    return true;
                }
                else
                {
                    // If for some reason we can't remove all N empty bottom rows, let's try N-1.
                    // I should create a unit test for this scenario because it's hard to articulate
                    // why I think this is desirable (so this could easily be a bug).
                    shiftAmount--;
                }

                // Revert the ShiftDown!
                grid.CopyFrom(winner.InitialGrid);
            }

            return false;
        }

        public ISpawnDeck MakeDeck() => new FixedSpawnDeck(Moves.Select(x => x.SpawnItem).ToList());

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

        public static IReadOnlyList<Puzzle> FindRawPuzzles(Ticker ticker, IReadOnlyList<Stamped<Command>> commands)
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

            private void State_OnComboCompleted(object? sender, ComboInfo combo)
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

        public static Puzzle? Solve(IImmutableGrid initialGrid, IReadOnlyList<Move> moves)
        {
            var result = SolveAndReturnGrid(initialGrid, moves);
            if (result.HasValue)
            {
                return new Puzzle(initialGrid, moves, result.Value.Item2);
            }
            return null;
        }

        public static (Grid, ComboInfo)? SolveAndReturnGrid(IImmutableGrid initialGrid, IReadOnlyList<Move> moves)
        {
            var grid = Grid.Clone(initialGrid);
            var combo = ComboInfo.Empty;

            foreach (var move in moves)
            {
                if (!grid.Place(move))
                {
                    return null;
                }
                if (move.DidBurst)
                {
                    grid.Burst();
                }
                grid.FallCompletely(Lists.DummyBuffer);

                combo = ComboInfo.Empty;
                while (grid.Destroy(ref combo))
                {
                    grid.FallCompletely(Lists.DummyBuffer);
                }
            }

            return (grid, combo);
        }

#if DEBUG
        /// <summary>
        /// For unit tests only. Creates a grid representing all the moves.
        /// Each horizontal drop will get one row to itself.
        /// Each vertical drop will get two rows to itself.
        /// </summary>
        public IReadOnlyGrid BuildMovesGrid()
        {
            var grid = Grid.Create(InitialGrid.Width, Moves.Count * 2); // At most each move will need two rows of the grid
            int y = 0;
            foreach (var move in Moves)
            {
                var mover = grid.NewMover(move.SpawnItem);
                mover = mover.JumpTo(move.Orientation);
                mover = mover.Jump(0, y - Math.Min(mover.LocA.Y, mover.LocB.Y));
                y += mover.LocA.Y == mover.LocB.Y ? 1 : 2;
                grid.Set(mover.LocA, mover.OccA);
                grid.Set(mover.LocB, mover.OccB);
            }
            return grid;
        }
#endif
    }
}
