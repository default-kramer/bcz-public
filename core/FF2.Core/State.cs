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
        private readonly HealthManager todo;

        public IHealthGrid HealthGrid => todo.Grid;

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
            var ps = PenaltySchedule.BasicIndexedSchedule(5 * 1000);
            todo = new HealthManager(PayoutTable.DefaultHealthPayoutTable, ps);
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

    class HealthManager
    {
        const int PenaltyAreaStart = 0;
        const int PenaltyAreaEnd = 4;
        // 5-6 is a "DMZ" of sorts, which each attack always has to cross regardless of penalties
        const int HealthStart = 7;
        const int HealthEnd = 14;
        const int PartialHealthRow = 15;

        const int W = 4;
        const int H = PartialHealthRow + 1;

        const int HealthPerRow = 2;
        const int MaxPenaltiesPerColumn = PenaltyAreaEnd - PenaltyAreaStart + 1;
        const int MaxHealth = (HealthEnd - HealthStart + 1) * HealthPerRow;
        const int MaxPartialHealth = 8; // 2 hearts * 4 fragments/heart
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
            new AttackShape(new Loc(1, HealthStart), 0),
            new AttackShape(new Loc(3, HealthStart), 2),
            new AttackShape(new Loc(0, HealthStart), 1),
            new AttackShape(new Loc(2, HealthStart), 3),
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
        private readonly HealthGrid healthGrid;
        private int totalWaitingMillis = 0;

        public HealthManager(PayoutTable healthPayoutTable, PenaltySchedule penaltySchedule)
        {
            this.healthPayoutTable = healthPayoutTable;
            this.penaltySchedule = penaltySchedule;

            penaltyLevels[0] = 3;
            penaltyLevels[1] = 4;
            penaltyLevels[2] = 6;
            penaltyLevels[3] = 8;
            penaltyCounts.AsSpan().Fill(0);

            CurrentAttack = RebuildAttack(HealthStart * AttackPerCell, Shapes[shapeIndex]);
            Health = MaxHealth;
            PartialHealth = 0;

            this.healthGrid = new HealthGrid(this);
            healthGrid.Redraw();
        }

        private static (int, Loc, float) RebuildAttack(int attackProgress, AttackShape shape)
        {
            int blocksAway = attackProgress / AttackPerCell;
            float subposition = (attackProgress % AttackPerCell) / AttackPerBlockFloat;
            var loc = new Loc(shape.AttackColumn, shape.HealthStartLoc.Y - blocksAway);
            return (attackProgress, loc, -subposition);
        }

        public IHealthGrid Grid => healthGrid;

        public void OnComboCompleted(ComboInfo combo)
        {
            RestoreHealth(combo.ComboToReward);
            RemovePenalties(combo.ComboToReward.AdjustedGroupCount);
            healthGrid.Redraw();
        }

        private void RestoreHealth(Combo combo)
        {
            int payout = healthPayoutTable.GetPayout(combo.AdjustedGroupCount);
            PartialHealth = PartialHealth + payout;
            while (PartialHealth >= MaxPartialHealth)
            {
                Health += 2;
                PartialHealth -= MaxPartialHealth;
            }

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
                attackProgress = (HealthStart - penaltyHeight) * AttackPerCell;
            }
            CurrentAttack = RebuildAttack(attackProgress, Shapes[shapeIndex]);

            if (penaltySchedule.TryAdvance(totalWaitingMillis, out var penalty))
            {
                AddPenalty(penalty.Level);
            }

            healthGrid.Redraw();
        }

        private void AddPenalty(int index)
        {
            int next = penaltyCounts[index] + 1;
            if (next <= MaxPenaltiesPerColumn)
            {
                penaltyCounts[index] = next;
            }
        }

        public bool GameOver => Health <= 0;

        class HealthGrid : GridBase, IHealthGrid
        {
            private readonly HealthManager manager;

            public HealthGrid(HealthManager manager) : base(W, H)
            {
                this.manager = manager;
            }

            public void Redraw()
            {
                this.Clear();

                // Place penalties
                for (int x = 0; x < W; x++)
                {
                    int yLimit = manager.penaltyCounts[x];
                    for (int y = 0; y < yLimit; y++)
                    {
                        Put(new Loc(x, y), HealthOccupants.Penalty);
                    }
                }

                // Place hearts
                var shape = Shapes[manager.shapeIndex];
                var loc = shape.HealthStartLoc;
                int partialHealthOffset = 0;
                if (shape.AttackColumn > 1)
                {
                    // The health is gone, but we keep showing the heart until the row is destroyed:
                    Put(loc.Add(-2, 0), HealthOccupants.Heart);
                    // And show the attack that already landed
                    Put(new Loc(shape.AttackColumn - 2, HealthStart), HealthOccupants.Attack);
                }
                for (int i = 0; i < manager.Health && loc.Y < H; i++)
                {
                    Put(loc, HealthOccupants.Heart);
                    partialHealthOffset = (loc.X + 1) % 2;
                    loc = loc.X switch
                    {
                        0 => loc.Add(2, 0),
                        1 => loc.Add(2, 0),
                        2 => loc.Add(-1, 1),
                        3 => loc.Add(-3, 1),
                        _ => throw new Exception("X should never exceed " + W),
                    };
                }

                // Place partial hearts
                var partialLoc = new Loc(partialHealthOffset, PartialHealthRow);
                Put(partialLoc, GetPartialHeart(manager.PartialHealth));
                Put(partialLoc.Add(2, 0), GetPartialHeart(manager.PartialHealth - 4));

                // Place current attack
                Put(manager.CurrentAttack.position, HealthOccupants.Attack);
            }

            private static Occupant GetPartialHeart(int partialHealth)
            {
                if (partialHealth == 1) { return HealthOccupants.Heart25; }
                else if (partialHealth == 2) { return HealthOccupants.Heart50; }
                else if (partialHealth == 3) { return HealthOccupants.Heart75; }
                else if (partialHealth >= 4) { return HealthOccupants.Heart100; }
                return HealthOccupants.Heart0;
            }

            public float GetAdder(Loc loc)
            {
                var attack = manager.CurrentAttack;
                if (loc == attack.position)
                {
                    return attack.subposition;
                }
                return 0f;
            }

            public override IImmutableGrid MakeImmutable()
            {
                throw new NotImplementedException();
            }
        }
    }
}
