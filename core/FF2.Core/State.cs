using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        GameOver = 5,

        // Values larger than 100 are not used by the State class.
        // It's just convenient to add them to this enum.
        Bursting = 101,

        // To be used as an "assertion failed" state:
        Unreachable = int.MaxValue,
    }

    // Does timing-related stuff belong in the state class? Or at a higher level?
    // Meh, I don't know, so just start hacking and we can always refactor later
    public sealed class State
    {
        private readonly Grid grid;
        private readonly FallAnimationSampler fallSampler;
        private readonly ISpawnDeck spawnDeck;
        private Mover? mover;
        private ComboInfo currentCombo;
        private int waitingMillis = 0;
        private Moment lastMoment = new Moment(0); // TODO should see about enforcing "cannot change Kind without providing a Moment"
        private int score = 0;
        private readonly PayoutTable scorePayoutTable = PayoutTable.DefaultScorePayoutTable;
        private readonly TODO todo;

        public IHealthGrid HealthGrid => todo.HealthGrid;

        public int Score => score;

        /// <summary>
        /// Holds the action that will be attempted on the next <see cref="Tick"/>.
        /// </summary>
        public StateKind Kind { get; private set; }

        /// <summary>
        /// TODO try not to use this? Or think about it more ...
        /// </summary>
        internal Moment Moment { get { return lastMoment; } }

        internal FallAnimationSampler FallSampler => fallSampler;

        public bool ClearedAllEnemies { get; private set; }

        public delegate void EventHandler<T>(State sender, T args);

        // Not sure how I feel about using events here...
        // Be careful to avoid subscribing to something that will live longer than you expect.
        public event EventHandler<ComboInfo>? OnComboCompleted;
        public event EventHandler<SpawnItem>? OnCatalystSpawned;

        public State(Grid grid, ISpawnDeck spawnDeck)
        {
            this.grid = grid;
            this.fallSampler = new FallAnimationSampler(grid);
            this.spawnDeck = spawnDeck;
            mover = null;
            Kind = StateKind.Spawning;
            currentCombo = ComboInfo.Empty;
            var ps = PenaltySchedule.BasicIndexedSchedule(2000);
            todo = new TODO(PayoutTable.DefaultHealthPayoutTable, ps);
        }

        public IReadOnlyGrid Grid { get { return grid; } }

        public ITickCalculations TickCalculations => grid.TickCalc;

        public static State Create(SeededSettings ss)
        {
            var spawns = ss.Settings.SpawnBlanks ? Lists.MainDeck : Lists.BlanklessDeck;
            var deck = new InfiniteSpawnDeck(spawns, new PRNG(ss.Seed));
            var grid = Core.Grid.Create(ss.Settings, new PRNG(ss.Seed));
            return new State(grid, deck);
        }

        public Viewmodels.QueueModel MakeQueueModel()
        {
            return new Viewmodels.QueueModel(this.spawnDeck);
        }

        public Viewmodels.PenaltyModel MakePenaltyModel(Ticker ticker)
        {
            var penalties = new PenaltyManager(10); // TODO
            return new Viewmodels.PenaltyModel(penalties, this, ticker);
        }

        public void Elapse(Moment now)
        {
            Elapse(now.Millis - lastMoment.Millis);
            lastMoment = now;
        }

        private void Elapse(int millis)
        {
            if (millis <= 0)
            {
                return;
            }

            if (Kind == StateKind.Waiting)
            {
                waitingMillis += millis;

                todo.OnWaitingFrame(millis);
                if (todo.GameOver)
                {
                    ChangeKind(true, StateKind.GameOver, StateKind.GameOver);
                    return;
                }
            }
        }

        public bool HandleCommand(Command command, Moment now)
        {
            Elapse(now);

            return command switch
            {
                Command.Left => Move(Direction.Left),
                Command.Right => Move(Direction.Right),
                Command.RotateCW => Rotate(clockwise: true),
                Command.RotateCCW => Rotate(clockwise: false),
                Command.Plummet => Plummet(),
                _ => throw new Exception($"Bad command: {command}"),
            };
        }

        public int RemainingMillis { get { return int.MaxValue; } } // TODO

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

            if (spawnDeck.PeekLimit < 1)
            {
                // Puzzle mode can exhaust the spawn deck.
                return false;
            }

            Slowmo = false;
            var colors = spawnDeck.Pop();
            mover = grid.NewMover(colors);
            OnCatalystSpawned?.Invoke(this, colors);
            return true;
        }

        public Command? Approach(Orientation o)
        {
            return mover?.Approach(o);
        }

        private bool Destroy()
        {
            var newCombo = currentCombo;
            var result = grid.Destroy(ref newCombo);
            if (result)
            {
                currentCombo = newCombo;
            }
            else
            {
                if (currentCombo.TotalNumGroups > 0)
                {
                    var scorePayout = GetHypotheticalScore(currentCombo);
                    score += scorePayout;
                    //Console.WriteLine($"Score: {score} (+{scorePayout})");
                    todo.OnComboCompleted(currentCombo);
                    OnComboCompleted?.Invoke(this, currentCombo);
                }
                currentCombo = ComboInfo.Empty;
            }
            Slowmo = Slowmo || result;
            return result;
        }

        /// <summary>
        /// If the given <paramref name="combo"/> were played, how much would it score?
        /// </summary>
        public int GetHypotheticalScore(ComboInfo combo)
        {
            return scorePayoutTable.GetPayout(combo.ComboToReward.AdjustedGroupCount);
        }

        /// <summary>
        /// Return false if nothing changes, or if <see cref="Kind"/> is the only thing that changes.
        /// Otherwise return true after executing some "significant" change.
        /// </summary>
        public bool Tick(Moment now)
        {
            grid.PreTick();
            var retval = DoTick(now);
            if (retval && grid.Stats.EnemyCount == 0)
            {
                // Don't change to GameOver immediately. Let the combo resolve.
                ClearedAllEnemies = true;
            }
            return retval;
        }

        private bool DoTick(Moment now)
        {
            if (Kind == StateKind.GameOver)
            {
                return false;
            }

            Elapse(now);

            if (ClearedAllEnemies && Kind == StateKind.Spawning)
            {
                // Now that the combo is resolved, we can transition to the GameOver state.
                return ChangeKind(true, StateKind.GameOver, StateKind.GameOver);
            }

            // Normal flow:
            switch (Kind)
            {
                case StateKind.Spawning:
                    // During a normal game, Spawn() should never fail.
                    // In puzzle mode, Spawn() will fail when the deck runs out
                    // and when that happens we transition to GameOver.
                    return ChangeKind(Spawn(), StateKind.Waiting, StateKind.GameOver);
                case StateKind.Waiting:
                    return false;
                case StateKind.Falling:
                    return ChangeKind(Fall(true), StateKind.Falling, StateKind.Destroying);
                case StateKind.Destroying:
                    return ChangeKind(Destroy(), StateKind.Falling, StateKind.Spawning);
                default:
                    throw new Exception("Unexpected kind: " + Kind);
            }
        }

        private bool Fall(bool completely)
        {
            var fallCountBuffer = fallSampler.ResetFallCountBuffer();

            bool result = grid.Fall(fallCountBuffer);
            bool keepGoing = completely && result;
            while (keepGoing)
            {
                keepGoing = grid.Fall(fallCountBuffer);
            }
            Slowmo = Slowmo || result;
            return result;
        }

        /// <summary>
        /// For cosmetic use only! Supports slow motion on the UI.
        /// </summary>
        public bool Slowmo { get; private set; }

        public Mover? PreviewPlummet()
        {
            return mover?.PreviewPlummet(grid);
        }

        public Move PreviousMove { get; private set; }

        private bool Plummet()
        {
            if (Kind != StateKind.Waiting || mover == null)
            {
                return false;
            }

            var m2 = mover.Value.PreviewPlummet(Grid);
            if (m2.HasValue)
            {
                var m = m2.Value;
                PreviousMove = m.GetMove(didBurst: false);
                grid.Set(m.LocA, m.OccA);
                grid.Set(m.LocB, m.OccB);
                mover = null;
                Kind = StateKind.Falling;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Burst(Moment now)
        {
            Elapse(now);
            grid.Burst();
            PreviousMove = new Move(PreviousMove.Orientation, PreviousMove.SpawnItem, didBurst: true);
        }

        private bool ChangeKind(bool significantChange, StateKind a, StateKind b)
        {
            Kind = significantChange ? a : b;
            return significantChange;
        }

        private bool Move(Direction dir)
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

        private bool Rotate(bool clockwise)
        {
            if (!mover.HasValue)
            {
                return false;
            }
            mover = mover.Value.Rotate(clockwise, grid.Width);
            return true;
        }

        public int HashGrid()
        {
            return grid.HashGrid();
        }
    }

    class TODO
    {
        const int W = 4;
        const int H = 16;
        const int MaxPartialHealth = 8;
        const int HealthY = 8;
        const int MaxPenaltyY = HealthY - 1;
        const int MaxHealth = 16;
        const int AttackPerCell = 1000 * 1000;
        const float AttackPerBlockFloat = AttackPerCell;

        // Could be a variable:
        const int AttackPerMillis = AttackPerCell / 1000;

        readonly struct AttackShape
        {
            public readonly Loc HealthStartLoc;
            public readonly int AttackColumn;

            public AttackShape(Loc a, int b) { this.HealthStartLoc = a; this.AttackColumn = b; }
        }

        private static readonly AttackShape[] Shapes = new[]
        {
            new AttackShape(new Loc(1, HealthY), 0),
            new AttackShape(new Loc(3, HealthY), 2),
            new AttackShape(new Loc(0, HealthY), 1),
            new AttackShape(new Loc(2, HealthY), 3),
        };

        private int shapeIndex = 0;

        // Progress counts down to zero
        private (int Progress, Loc position, float subposition) CurrentAttack;

        private int Health;
        private int PartialHealth;
        private readonly int[] penaltyLevels = new int[W];
        private readonly int[] penaltyCounts = new int[W];
        private readonly PayoutTable healthPayoutTable;
        private PenaltySchedule penaltySchedule;
        private readonly IHealthGrid healthGrid;
        private int totalWaitingMillis = 0;

        public TODO(PayoutTable healthPayoutTable, PenaltySchedule penaltySchedule)
        {
            this.healthPayoutTable = healthPayoutTable;
            this.penaltySchedule = penaltySchedule;

            penaltyLevels[0] = 2;
            penaltyLevels[1] = 3;
            penaltyLevels[2] = 4;
            penaltyLevels[3] = 5;
            penaltyCounts.AsSpan().Fill(0);

            CurrentAttack = RebuildAttack(HealthY * AttackPerCell, Shapes[shapeIndex]);
            Health = MaxHealth;
            PartialHealth = 0;

            this.healthGrid = new GridRep(this);
        }

        private static (int, Loc, float) RebuildAttack(int attackProgress, AttackShape shape)
        {
            int blocksAway = attackProgress / AttackPerCell;
            float subposition = (attackProgress % AttackPerCell) / AttackPerBlockFloat;
            var loc = new Loc(shape.AttackColumn, shape.HealthStartLoc.Y - blocksAway);
            return (attackProgress, loc, -subposition);
        }

        public IHealthGrid HealthGrid => healthGrid;

        public void OnComboCompleted(ComboInfo combo)
        {
            RestoreHealth(combo.ComboToReward);
            RemovePenalties(combo.ComboToReward.AdjustedGroupCount);
        }

        private void RestoreHealth(Combo combo)
        {
            int payout = healthPayoutTable.GetPayout(combo.AdjustedGroupCount);
            PartialHealth = PartialHealth + payout;
            Health += PartialHealth / MaxPartialHealth;
            PartialHealth = PartialHealth % MaxPartialHealth;

            if (Health >= MaxHealth)
            {
                PartialHealth = 0; // No room to accumulate lost health while at full health
            }
        }

        private void RemovePenalties(int N)
        {
            int index = W - 1;
            while (index >= 0)
            {
                if (penaltyLevels[index] <= N)
                {
                    Remove2(index);
                    return;
                }
                index--;
            }
        }

        private void Remove2(int index)
        {
            int minCount = penaltyCounts[index];

            while (index >= 0)
            {
                int count = penaltyCounts[index];
                if (count > 0 && count >= minCount)
                {
                    penaltyCounts[index] = count - 1;
                }
                index--;
            }
        }

        public void OnWaitingFrame(int millis)
        {
            // TODO should be a data type that holds "elapsed" and "total"
            totalWaitingMillis += millis;

            var attackProgress = CurrentAttack.Progress - millis * AttackPerMillis;
            if (attackProgress <= 0)
            {
                Health--;
                shapeIndex = (shapeIndex + 1) % Shapes.Length;
                var penaltyHeight = penaltyCounts.Max();
                attackProgress = (HealthY - penaltyHeight) * AttackPerCell;
            }
            CurrentAttack = RebuildAttack(attackProgress, Shapes[shapeIndex]);

            if (penaltySchedule.TryAdvance(totalWaitingMillis, out var penalty))
            {
                AddPenalty(penalty.Level);
            }
        }

        private void AddPenalty(int index)
        {
            int next = penaltyCounts[index] + 1;
            if (next <= MaxPenaltyY)
            {
                penaltyCounts[index] = next;
            }
        }

        public bool GameOver => Health <= 0;

        class GridRep : IHealthGrid, IReadOnlyGrid
        {
            private readonly TODO todo;

            public GridRep(TODO todo)
            {
                this.todo = todo;
            }

            public int Width => W;

            public int Height => H;

            public GridSize Size => new GridSize(W, H);

            public string PrintGrid => throw new NotImplementedException();

            public string DiffGridString(params string[] rows)
            {
                throw new NotImplementedException();
            }

            private static readonly Occupant penaltyOcc = Occupant.MakeCatalyst(Color.Blue, Direction.Up);

            public Occupant Get(Loc loc)
            {
                // Check for an attack
                if (todo.CurrentAttack.position == loc)
                {
                    return Occupant.MakeCatalyst(Color.Blank, Direction.None);
                }

                // Check for a penalty
                var penaltyHeight = todo.penaltyCounts[loc.X];
                if (loc.Y < penaltyHeight)
                {
                    return penaltyOcc;
                }

                // Check for a heart
                if (todo.Health <= 0)
                {
                    return Occupant.None;
                }
                var healthStartLoc = TODO.Shapes[todo.shapeIndex].HealthStartLoc;
                if (loc == healthStartLoc.Add(-2, 0))
                {
                    return Occupant.MakeCatalyst(Color.Red, Direction.Left); // Debugging, should just be a regular heart
                }
                int xOffset = loc.X - healthStartLoc.X;
                int yOffset = loc.Y - healthStartLoc.Y;
                int totalOffset = xOffset + yOffset * W + yOffset % 2;
                if (totalOffset < 0 || totalOffset % 2 != 0)
                {
                    return Occupant.None;
                }
                if (todo.Health >= totalOffset / 2)
                {
                    return Occupant.IndestructibleEnemy;
                }
                return Occupant.None;
            }

            public float GetAdder(Loc loc)
            {
                var attack = todo.CurrentAttack;
                if (loc == attack.position)
                {
                    return attack.subposition;
                }
                return 0f;
            }

            public int HashGrid()
            {
                throw new NotImplementedException();
            }

            // TODO extension method:
            public bool InBounds(Loc loc)
            {
                return loc.X >= 0 && loc.X < W && loc.Y >= 0 && loc.Y < H;
            }

            // TODO extension method:
            public bool IsVacant(Loc loc)
            {
                return Get(loc) == Occupant.None;
            }

            public IImmutableGrid MakeImmutable()
            {
                throw new NotImplementedException();
            }

            public Mover NewMover(SpawnItem item)
            {
                throw new NotImplementedException();
            }

            public ReadOnlySpan<Occupant> ToSpan()
            {
                throw new NotImplementedException();
            }
        }
    }
}
