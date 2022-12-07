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
        private readonly StateEvent.Factory eventFactory;
        private readonly Timekeeper timekeeper;
        private readonly IScheduler scheduler;
        private StateEvent __currentEvent = StateEvent.StateConstructed;
        public StateEvent CurrentEvent => __currentEvent;

        public int Score => score;

        public int NumCatalystsSpawned = 0; // Try not to use this...

        /// <summary>
        /// Holds the action that will be attempted on the next <see cref="Tick"/>.
        /// </summary>
        public StateKind Kind
        {
            get
            {
                switch (CurrentEvent.Kind)
                {
                    case StateEventKind.StateConstructed:
                    case StateEventKind.PenaltyAdded:
                        return StateKind.Spawning;
                    case StateEventKind.Spawned:
                        return StateKind.Waiting;
                    case StateEventKind.Fell:
                        return StateKind.Destroying;
                    case StateEventKind.Destroyed:
                    case StateEventKind.Plummeted:
                    case StateEventKind.BurstBegan:
                        return StateKind.Falling;
                    case StateEventKind.GameEnded:
                        return StateKind.GameOver;
                    default:
                        throw new Exception($"Unexpected kind: {CurrentEvent.Kind}");
                }
            }
        }

        public bool ClearedAllEnemies { get; private set; }

        public delegate void EventHandler<T>(State sender, T args);

        // Not sure how I feel about using events here...
        // Be careful to avoid subscribing to something that will live longer than you expect.
        public event EventHandler<ComboInfo>? OnComboCompleted;
        public event EventHandler<SpawnItem>? OnCatalystSpawned;

        public static State CreateWithInfiniteHealth(Grid grid, ISpawnDeck deck)
        {
            return new State(grid, deck, NullStateHook.Instance);
        }

        internal State(Grid grid, ISpawnDeck spawnDeck, IStateHook hook)
        {
            this.grid = grid;
            this.fallSampler = new FallAnimationSampler(grid);
            this.spawnDeck = spawnDeck;
            this.eventFactory = new();
            this.timekeeper = new();
            this.scheduler = timekeeper;
            mover = null;
            currentCombo = ComboInfo.Empty;
            this.hook = hook;
            if (hook is HealthV2 hv2)
            {
                TEMP = hv2;
                PENALTY_LEFT = TEMP.MakePenaltyGrid(true);
                PENALTY_RIGHT = TEMP.MakePenaltyGrid(false);
            }
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

            IStateHook hook = ss.Settings.InfiniteHealth
                ? NullStateHook.Instance
                : new HealthV2(deck);

            return new State(grid, deck, hook);
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

            Transition();

            if (hook.GameOver)
            {
                Update(StateEvent.GameEnded);
            }
        }

        public bool HandleCommand(Command command, Moment now)
        {
            Elapse(now);

            switch (command)
            {
                case Command.Left:
                    return Move(Direction.Left);
                case Command.Right:
                    return Move(Direction.Right);
                case Command.RotateCW:
                    return Rotate(clockwise: true);
                case Command.RotateCCW:
                    return Rotate(clockwise: false);
                case Command.Plummet:
                    return MaybeUpdate(Plummet());
                case Command.BurstBegin:
                    return MaybeUpdate(BurstBegin());
                case Command.BurstCancel:
                    return MaybeUpdate(BurstCancel());
                default:
                    throw new Exception($"Bad command: {command}");
            }
        }

        private bool MaybeUpdate(StateEvent? result)
        {
            if (result == null)
            {
                return false;
            }
            __currentEvent = result.Value;
            return true;
        }

        [Obsolete("Argument is no longer nullable. Call Update() instead.")]
        private bool MaybeUpdate(StateEvent result)
        {
            __currentEvent = result;
            return true;
        }

        private bool Update(StateEvent result)
        {
            __currentEvent = result;
            return true;
        }

        public float LastGaspProgress() => 0; // TODO

        private StateEvent Spawn()
        {
            if (mover.HasValue)
            {
                throw new Exception("State got hosed: mover already exists");
            }

            if (ClearedAllEnemies)
            {
                // Now that the combo is resolved, we can transition to the GameOver state.
                return StateEvent.GameEnded;
            }

            if (spawnDeck.PeekLimit < 1)
            {
                // Puzzle mode can exhaust the spawn deck.
                return StateEvent.GameEnded;
            }

            Slowmo = false;
            var spawnItem = spawnDeck.Pop();
            if (spawnItem.IsCatalyst(out var _))
            {
                NumCatalystsSpawned++;
                mover = grid.NewMover(spawnItem);
                OnCatalystSpawned?.Invoke(this, spawnItem);
                return eventFactory.Spawned(spawnItem, scheduler.CreateAppointment(150));
            }
            else if (spawnItem.IsPenalty())
            {
                var result = hook.AddPenalty(spawnItem, eventFactory, scheduler);
                // If the hook returned null, we just ignore the penalty and try again
                return result ?? Spawn();
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
                    hook.OnComboCompleted(currentCombo, scheduler); // TODO should be passed into Destroy() here
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

        private bool Transition()
        {
            bool retval = __SingleTransition();
            while (__SingleTransition()) { }
            if (retval && grid.Stats.EnemyCount == 0)
            {
                // Don't change to GameOver immediately. Let the combo resolve.
                ClearedAllEnemies = true;
            }
            return retval;
        }

        private bool __SingleTransition()
        {
            if (!CurrentEvent.Completion.HasArrived())
            {
                return false;
            }
            else if (CurrentEvent.Kind == StateEventKind.Destroyed)
            {
                grid.ResetDestructionCalculations();
            }

            switch (CurrentEvent.Kind)
            {
                case StateEventKind.StateConstructed:
                case StateEventKind.PenaltyAdded:
                    return Update(Spawn());
                case StateEventKind.Spawned:
                    return false;
                case StateEventKind.Fell:
                case StateEventKind.Destroyed:
                case StateEventKind.Plummeted:
                    return Update(FallOrDestroyOrSpawn());
                case StateEventKind.GameEnded:
                    return false;
                case StateEventKind.BurstBegan:
                    Burst();
                    return Update(FallOrDestroyOrSpawn());
                default:
                    throw new Exception("TODO");
            }
        }

        private StateEvent FallOrDestroyOrSpawn()
        {
            if (Fall(true))
            {
                int millis = 150 * fallSampler.MaxFall();
                return eventFactory.Fell(fallSampler, scheduler.CreateAppointment(millis));
            }
            else if (Destroy())
            {
                return eventFactory.Destroyed(TickCalculations, scheduler.CreateAppointment(550));
            }
            else
            {
                return Spawn();
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

        public Move? PreviousMove { get; private set; } = null;

        private StateEvent? BurstBegin()
        {
            if (!Plummet_NoEvent())
            {
                return null;
            }

            return eventFactory.BurstBegan(scheduler.CreateAppointment(500));
        }

        private StateEvent? BurstCancel()
        {
            if (CurrentEvent.Kind != StateEventKind.BurstBegan)
            {
                return null;
            }

            return eventFactory.Plummeted(scheduler.CreateAppointment(0));
        }

        private bool Plummet_NoEvent()
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
                return true;
            }
            else
            {
                return false;
            }
        }

        private StateEvent? Plummet()
        {
            if (Plummet_NoEvent())
            {
                return eventFactory.Plummeted(scheduler.CreateAppointment(0));
            }
            return null;
        }

        private void Burst()
        {
            grid.Burst();
            var pm = PreviousMove!.Value;
            PreviousMove = new Move(pm.Orientation, pm.SpawnItem, didBurst: true);
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
            attack = new Attack(leftSide, startingHeight, scheduler.CreateWaitingAppointment(remain * 400));
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
        public StateEvent? AddPenalty(SpawnItem penalty, StateEvent.Factory factory, IScheduler scheduler)
        {
            var payload = new PenaltyAddedInfo(addToLeft, 2);

            if (addToLeft)
            {
                leftPenalty = leftPenalty.Increment();
            }
            else
            {
                rightPenalty = rightPenalty.Increment();
            }
            addToLeft = !addToLeft;

            return factory.PenaltyAdded(payload, scheduler.CreateAppointment(1000));
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

            public int Width => 2;

            public int Height => GridHeight;

            public GridSize Size => new GridSize(Width, Height);

            public string PrintGrid => throw new NotImplementedException();

            public string DiffGridString(params string[] rows)
            {
                throw new NotImplementedException();
            }

            public Occupant Get(Loc loc)
            {
                bool outer = loc.X == (left ? 0 : 1);
                if (outer)
                {
                    var ps = left ? health.leftPenalty : health.rightPenalty;
                    if (loc.Y < ps.PenaltyCount * ps.HeightPerPenalty)
                    {
                        return HealthOccupants.Penalty;
                    }
                }
                else if (left == health.attack.LeftSide)
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

    public readonly struct PenaltyAddedInfo
    {
        public readonly bool LeftSide;
        public readonly int Height;

        public PenaltyAddedInfo(bool left, int height)
        {
            this.LeftSide = left;
            this.Height = height;
        }
    }
}
