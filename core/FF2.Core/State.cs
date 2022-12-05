using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

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
        private int score = 0;
        private readonly PayoutTable scorePayoutTable = PayoutTable.DefaultScorePayoutTable;
        private readonly IStateHook hook;

        public int Score => score;

        /// <summary>
        /// Holds the action that will be attempted on the next <see cref="Tick"/>.
        /// </summary>
        public StateKind Kind { get; private set; }

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
            TEMP = new HealthV2(spawnDeck);
            hook = TEMP;
            PENALTY_LEFT = TEMP.MakePenaltyGrid(true);
            PENALTY_RIGHT = TEMP.MakePenaltyGrid(false);
        }

        private readonly HealthV2 TEMP;
        public readonly IReadOnlyGrid PENALTY_LEFT;
        public readonly IReadOnlyGrid PENALTY_RIGHT;
        public RestoreHealthAnimation RestoreHealthAnimation => TEMP.RestoreHealthAnimation;

        public int CurrentHealth => TEMP.CurrentHealth;

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

        // TODO this should probably go away ...
        public void Elapse(Moment now)
        {
            timekeeper.Elapse(now, this);
        }

        // ... and this should become non-TEMP code.
        internal void TEMP_TimekeeperHook(IScheduler scheduler)
        {
            hook.Elapse(scheduler);

            if (hook.GameOver)
            {
                ChangeKind(true, StateKind.GameOver, StateKind.GameOver);
            }
        }

        private readonly Timekeeper timekeeper = new Timekeeper();

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

        public float LastGaspProgress() => 0; // TODO

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
            var spawnItem = spawnDeck.Pop();
            if (spawnItem.IsCatalyst(out var _))
            {
                mover = grid.NewMover(spawnItem);
                OnCatalystSpawned?.Invoke(this, spawnItem);
                return true;
            }
            else if (spawnItem.IsPenalty())
            {
                hook.AddPenalty(spawnItem);
                return Spawn(); // TODO gonna want some animation here...
            }
            else
            {
                throw new Exception($"Cannot spawn: {spawnItem}");
            }
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
                    hook.OnComboCompleted(currentCombo, timekeeper); // TODO should be passed into Destroy() here
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

        public Mover? GetMover => mover;

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

    sealed class HealthV2 : IStateHook
    {
        readonly struct PenaltyStatus
        {
            public readonly int AGCToClear;
            public readonly int HeightPerPenalty;
            public readonly int PenaltyCount;

            public PenaltyStatus(int agc, int height, int count)
            {
                this.AGCToClear = agc;
                this.HeightPerPenalty = height;
                this.PenaltyCount = count;
            }

            public PenaltyStatus MaybeDecrement(int agc)
            {
                if (agc >= AGCToClear && PenaltyCount > 0)
                {
                    return new PenaltyStatus(AGCToClear, HeightPerPenalty, PenaltyCount - 1);
                }
                return this;
            }

            public PenaltyStatus Increment()
            {
                return new PenaltyStatus(AGCToClear, HeightPerPenalty, PenaltyCount + 1);
            }
        }

        readonly struct Attack
        {
            public readonly bool LeftSide;
            public readonly int StartingHeight;
            public readonly Appointment HitTime;

            public Attack(bool leftSide, int startingHeight, Appointment hitTime)
            {
                this.LeftSide = leftSide;
                this.StartingHeight = startingHeight;
                this.HitTime = hitTime;
            }
        }

        const int GridHeight = 20;

        private int Health = 20;
        private PenaltyStatus leftPenalty;
        private PenaltyStatus rightPenalty;
        private Attack attack;
        private readonly PayoutTable healthPayoutTable;
        private readonly ISpawnDeck spawnDeck;
        private RestoreHealthAnimation restoreHealthAnimation; // For display only! Do not use in logic!

        public RestoreHealthAnimation RestoreHealthAnimation => restoreHealthAnimation;

        public int CurrentHealth => Health;

        public HealthV2(ISpawnDeck spawnDeck)
        {
            leftPenalty = new PenaltyStatus(3, 2, 1);
            rightPenalty = new PenaltyStatus(6, 2, 2);
            attack = new Attack(true, 0, Appointment.Frame0);
            healthPayoutTable = PayoutTable.DefaultHealthPayoutTable;
            this.spawnDeck = spawnDeck;
            restoreHealthAnimation = new RestoreHealthAnimation(0, Appointment.Frame0);
        }

        public bool GameOver => Health <= 0;

        public void Elapse(IScheduler scheduler)
        {
            if (attack.HitTime.IsFrame0)
            {
                StartNewAttack(scheduler, leftSide: true);
            }
            else if (attack.HitTime.HasArrived())
            {
                Health -= 4;
                StartNewAttack(scheduler, !attack.LeftSide);
            }
        }

        private void StartNewAttack(IScheduler scheduler, bool leftSide)
        {
            PenaltyStatus ps = leftSide ? leftPenalty : rightPenalty;
            int startingHeight = ps.PenaltyCount * ps.HeightPerPenalty;
            int remain = Math.Max(1, GridHeight - startingHeight);
            attack = new Attack(leftSide, 0, scheduler.CreateWaitingAppointment(remain * 400));
        }

        int comboCount = 0;

        public void OnComboCompleted(ComboInfo combo, IScheduler scheduler)
        {
            comboCount++;
            if (comboCount % 2 == 0)
            {
                spawnDeck.AddPenalty(SpawnItem.PENALTY); // For now, just add penalty every other destruction
            }

            // To ensure a finite game (as long as enemy count never increases), we use the Strict Combo
            // to get the payout. This means that you cannot gain health without destroying enemies.
            int gainedHealth = combo.NumEnemiesDestroyed + healthPayoutTable.GetPayout(combo.StrictCombo.AdjustedGroupCount);
            Health += gainedHealth;
            restoreHealthAnimation = new(gainedHealth, scheduler.CreateAppointment(gainedHealth * 200));

            int agc = combo.PermissiveCombo.AdjustedGroupCount;
            leftPenalty = leftPenalty.MaybeDecrement(agc);
            rightPenalty = rightPenalty.MaybeDecrement(agc);
        }

        public IReadOnlyGrid MakePenaltyGrid(bool left)
        {
            return new PenaltyGrid(this, left);
        }

        bool addToLeft = true;
        public void AddPenalty(SpawnItem penalty)
        {
            if (addToLeft)
            {
                leftPenalty = leftPenalty.Increment();
            }
            else
            {
                rightPenalty = rightPenalty.Increment();
            }
            addToLeft = !addToLeft;
        }

        sealed class PenaltyGrid : IReadOnlyGrid
        {
            private readonly HealthV2 health;
            private readonly bool left;

            public PenaltyGrid(HealthV2 parent, bool left)
            {
                this.health = parent;
                this.left = left;
            }

            public int Width => 1;

            public int Height => GridHeight;

            public GridSize Size => new GridSize(Width, Height);

            public string PrintGrid => throw new NotImplementedException();

            public string DiffGridString(params string[] rows)
            {
                throw new NotImplementedException();
            }

            public Occupant Get(Loc loc)
            {
                var ps = left ? health.leftPenalty : health.rightPenalty;
                if (loc.Y < ps.PenaltyCount * ps.HeightPerPenalty)
                {
                    return Occupant.IndestructibleEnemy;
                }

                if (loc.Y < ps.PenaltyCount * ps.HeightPerPenalty)
                {
                    return HealthOccupants.Penalty;
                }

                if (left == health.attack.LeftSide)
                {
                    float adder = health.attack.HitTime.Progress() * (Height - health.attack.StartingHeight);
                    if (loc.Y == Convert.ToInt32(health.attack.StartingHeight + adder))
                    {
                        return HealthOccupants.Attack;
                    }
                }

                return Occupant.None;
            }

            public int HashGrid()
            {
                throw new NotImplementedException();
            }

            public bool InBounds(Loc loc)
            {
                throw new NotImplementedException();
            }

            public bool IsVacant(Loc loc)
            {
                throw new NotImplementedException();
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

    // TODO do not check in. The viewmodel can do this itself if it has the scheduler available
    // Aha - and animation appointments do not need millisecond-perfection!
    // Also - this animation is not super obvious... Perhaps each quarter-heart should shoot in from
    // the side?
    public readonly struct RestoreHealthAnimation
    {
        public readonly int HealthGained;
        public readonly Appointment Appointment;

        public RestoreHealthAnimation(int healthGained, Appointment appointment)
        {
            this.HealthGained = healthGained;
            this.Appointment = appointment;
        }
    }
}
