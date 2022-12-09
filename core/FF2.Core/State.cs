using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core.Viewmodels;

namespace FF2.Core
{
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

        public bool IsWaitingOnUser => CurrentEvent.Kind == StateEventKind.Spawned;

        public bool IsGameOver => CurrentEvent.Kind == StateEventKind.GameEnded;

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
            if (hook is HealthManager hv2)
            {
                TEMP = hv2;
                PENALTY_LEFT = TEMP.LEFT_VM;
                PENALTY_RIGHT = TEMP.RIGHT_VM;
            }
        }

        private readonly HealthManager TEMP;
        public readonly Viewmodels.IPenaltyViewmodel PENALTY_LEFT;
        public readonly Viewmodels.IPenaltyViewmodel PENALTY_RIGHT;
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
                : new HealthManager(deck);

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
                hook.OnCatalystSpawned(spawnItem);
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
                var previous = currentCombo;
                this.currentCombo = newCombo;
                hook.OnComboUpdated(previous, newCombo, scheduler);
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
                int millis = Constants.FallingMillisPerCell * fallSampler.MaxFall();
                return eventFactory.Fell(fallSampler, scheduler.CreateAppointment(millis));
            }
            else if (Destroy())
            {
                return eventFactory.Destroyed(TickCalculations, scheduler.CreateAppointment(Constants.DestructionMillis));
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
            if (mover == null)
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
}
