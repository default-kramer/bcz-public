using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    public sealed class State : IDumpCallback
    {
        private readonly Grid grid;
        private readonly FallAnimationSampler fallSampler;
        private readonly ISpawnDeck spawnDeck;
        private Mover? mover;
        private ComboInfo currentCombo;
        public ComboInfo? ActiveOrPreviousCombo { get; private set; } = null;
        private int score = 0;
        private int numCombos = 0;
        private readonly PayoutTable scorePayoutTable = PayoutTable.DefaultScorePayoutTable;
        private readonly IStateHook hook;
        private readonly StateEvent.Factory eventFactory;
        private readonly Timekeeper timekeeper;
        private readonly IScheduler scheduler;
        private readonly DumpAnimator dumpAnimator;
        private readonly FallAnimator fallAnimator;
        private readonly IDestructionAnimator destructionAnimator;
        private readonly BarrierDestructionAnimator barrierDestructionAnimator;

        private StateEvent __currentEvent = StateEvent.StateConstructed;
        public StateEvent CurrentEvent => __currentEvent;

        public int Score => score;
        public int NumCombos => numCombos;

        public int NumCatalystsSpawned = 0; // Try not to use this...

        public bool IsWaitingOnUser => CurrentEvent.Kind == StateEventKind.Spawned;

        public bool IsGameOver => CurrentEvent.Kind == StateEventKind.GameEnded;

        public bool ClearedAllEnemies { get; private set; }

        public Moment TODO_ClearTime { get; private set; }

        public delegate void EventHandler<T>(State sender, T args);

        // Not sure how I feel about using events here...
        // Be careful to avoid subscribing to something that will live longer than you expect.
        public event EventHandler<ComboInfo>? OnComboLikelyCompleted;
        public event EventHandler<SpawnItem>? OnCatalystSpawned;

        public static State CreateWithInfiniteHealth(Grid grid, ISpawnDeck deck)
        {
            return new State(grid, deck, NullStateHook.Instance, new Timekeeper(), null, null, null);
        }

        internal State(Grid grid, ISpawnDeck spawnDeck, IStateHook makeHook, Timekeeper timekeeper,
            IAttackGridViewmodel? attackGridViewmodel,
            ISwitchesViewmodel? switchesViewmodel,
            ICountdownViewmodel? countdownViewmodel)
        {
            this.grid = grid;
            this.fallSampler = new FallAnimationSampler(grid);
            this.spawnDeck = spawnDeck;
            this.eventFactory = new();
            this.timekeeper = timekeeper;
            this.scheduler = timekeeper;
            dumpAnimator = new DumpAnimator(grid.Width, grid.Height + 2); // dump from the mover area
            fallAnimator = new FallAnimator(fallSampler, 0f);
            destructionAnimator = new StandardDestructionAnimator(this);
            barrierDestructionAnimator = new BarrierDestructionAnimator();
            mover = null;
            currentCombo = ComboInfo.Empty;
            this.hook = makeHook;
            this.AttackGridViewmodel = attackGridViewmodel;
            this.SwitchesViewmodel = switchesViewmodel;
            this.CountdownViewmodel = countdownViewmodel;
        }

        public readonly ICountdownViewmodel? CountdownViewmodel;
        public readonly IAttackGridViewmodel? AttackGridViewmodel;
        public readonly ISwitchesViewmodel? SwitchesViewmodel;

        public IReadOnlyGrid Grid { get { return grid; } }

        public ITickCalculations TickCalculations => grid.TickCalc;

        public static State Create(SeededSettings ss)
        {
            var spawns = ss.Settings.SpawnBlanks ? Lists.MainDeck : Lists.BlanklessDeck;
            var deck = new InfiniteSpawnDeck(spawns, new PRNG(ss.Seed));
            var grid = Core.Grid.Create(ss.Settings, new PRNG(ss.Seed));
            var timekeeper = new Timekeeper();

            var mode = ss.Settings.GameMode;
            if (mode == GameMode.PvPSim)
            {
                var switches = new Switches();
                var hook = new SimulatedAttacker(switches);
                return new State(grid, deck, hook, timekeeper, hook.VM, hook.SwitchVM, null);
            }
            else if (mode == GameMode.Levels)
            {
                var hook = new CountdownHook(timekeeper);
                return new State(grid, deck, hook, timekeeper, null, null, hook);
            }
            else
            {
                throw new Exception("Unsupported game mode: " + mode);
            }
        }

        public GoalArgs MakeGoalArgs()
        {
            return new GoalArgs(timekeeper.TODO_NOW.Millis, NumCatalystsSpawned);
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

        [Obsolete("Attention call site: Your argument is no longer nullable. You should call Update() instead.")]
        private bool MaybeUpdate(StateEvent result)
        {
            throw new Exception("I told you not to call this...");
        }

        private bool Update(StateEvent result)
        {
            __currentEvent = result;
            return true;
        }

        public float LastGaspProgress() => 0; // TODO

        private StateEvent Spawn()
        {
            if (currentCombo.PermissiveCombo.AdjustedGroupCount > 0)
            {
                var scorePayout = GetHypotheticalScore(currentCombo);
                score += scorePayout;
                numCombos++;
                //Console.WriteLine($"Score: {score} (+{scorePayout})");
            }
            currentCombo = ComboInfo.Empty;

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

            hook.PreSpawn(this, NumCatalystsSpawned);

            if (pendingDumps > 0)
            {
                pendingDumps = 0; // TODO need to decide how dump volume will be controlled...
                return Dump();
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
            else
            {
                throw new Exception($"Cannot spawn: {spawnItem}");
            }
        }

        int pendingDumps = 0;
        void IDumpCallback.Dump(int numAttacks)
        {
            pendingDumps += numAttacks;
        }

        private StateEvent Dump()
        {
            var appt = scheduler.CreateAppointment(Constants.DumpMillis);
            dumpAnimator.Reset(appt);

            for (int x = 0; x < grid.Width; x++)
            {
                DumpColumn(x);
            }

            return eventFactory.Dumped(appt);
        }

        private void DumpColumn(int x)
        {
            for (int y = grid.Height - 2; y >= 0; y--)
            {
                var loc = new Loc(x, y);
                var item = grid.Get(loc);
                if (item != Occupant.None)
                {
                    var nextColor = item.Color switch
                    {
                        Color.Red => Color.Yellow,
                        Color.Yellow => Color.Blue,
                        _ => Color.Red,
                    };
                    var dumpLoc = loc.Add(0, 1);
                    dumpAnimator.SetDumpLoc(dumpLoc);
                    grid.Set(dumpLoc, Occupant.MakeCatalyst(nextColor, Direction.None));
                    return;
                }
            }
        }

        private class DumpAnimator : IFallAnimator
        {
            const int NoDump = -1; // this Y coordinate does not exist
            private readonly int[] dumpLocs; // for each column, hold the Y coordinate that the dump lands in
            private readonly int height;
            private Appointment animation;

            public DumpAnimator(int width, int height)
            {
                dumpLocs = new int[width];
                this.height = height;
                Reset(Appointment.Frame0);
            }

            public void Reset(Appointment animation)
            {
                this.animation = animation;
                dumpLocs.AsSpan().Fill(NoDump);
            }

            public void SetDumpLoc(Loc loc)
            {
                dumpLocs[loc.X] = loc.Y;
            }

            public float GetAdder(Loc loc)
            {
                if (dumpLocs[loc.X] == loc.Y)
                {
                    var drop = height - loc.Y;
                    return drop - drop * animation.Progress();
                }
                return 0;
            }
        }

        public IFallAnimator GetFallAnimator()
        {
            var ev = CurrentEvent;
            var kind = ev.Kind;
            if (kind == StateEventKind.Fell)
            {
                float progress = ev.Completion.Progress();
                var sampler = ev.FellPayload();
                fallAnimator.Resample(sampler, progress);
                return fallAnimator;
            }
            else if (kind == StateEventKind.Dumped)
            {
                return dumpAnimator;
            }
            return NullFallAnimator.Instance;
        }

        public IDestructionAnimator GetDestructionAnimator()
        {
            var ev = CurrentEvent;
            if (ev.Kind == StateEventKind.BarrierDestroyed)
            {
                return barrierDestructionAnimator;
            }
            return destructionAnimator;
        }

        public Command? Approach(Orientation o)
        {
            return mover?.Approach(o);
        }

        private StateEvent DestroyBarriers(int y)
        {
            var gridSize = grid.Size;
            for (int x = 0; x < gridSize.Width; x++)
            {
                var loc = new Loc(x, y);
                grid.Set(loc, Occupant.None);
            }

            var appointment = scheduler.CreateAppointment(1000);
            barrierDestructionAnimator.Reset(y, gridSize.Width, appointment);
            return eventFactory.BarrierDestroyed(appointment);
        }

        private bool Destroy()
        {
            var newCombo = currentCombo;
            var result = grid.Destroy(ref newCombo);
            if (result)
            {
                var previous = currentCombo;
                this.currentCombo = newCombo;
                ActiveOrPreviousCombo = newCombo;
                hook.OnComboUpdated(previous, newCombo, scheduler);
            }
            else
            {
                if (currentCombo.TotalNumGroups > 0)
                {
                    hook.OnComboLikelyCompleted(this, currentCombo, scheduler);
                    OnComboLikelyCompleted?.Invoke(this, currentCombo);
                }
            }
            Slowmo = Slowmo || result;
            return result;
        }

        /// <summary>
        /// If the given <paramref name="combo"/> were played, how much would it score?
        /// </summary>
        public int GetHypotheticalScore(ComboInfo combo)
        {
            int enemyScore = combo.NumEnemiesDestroyed * 100;
            int comboScore = scorePayoutTable.GetPayout(combo.ComboToReward.AdjustedGroupCount);
            return enemyScore + comboScore;
        }

        private bool Transition()
        {
            bool retval = __SingleTransition();
            while (__SingleTransition()) { }
            if (retval && grid.Stats.EnemyCount == 0)
            {
                // Don't change to GameOver immediately. Let the combo resolve.
                ClearedAllEnemies = true;
                TODO_ClearTime = timekeeper.TODO_NOW;
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
                case StateEventKind.Dumped:
                case StateEventKind.BarrierDestroyed:
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

        class StandardDestructionAnimator : IDestructionAnimator
        {
            private readonly State state;

            public StandardDestructionAnimator(State state)
            {
                this.state = state;
            }

            public (Occupant, float) GetDestroyedOccupant(Loc loc)
            {
                if (!state.grid.InBounds(loc))
                {
                    // This bounds check is currently needed because the GridViewer is calling this with Mover locs,
                    // which do not exist on the main grid.
                    return (Occupant.None, 0f);
                }

                var ev = state.CurrentEvent;
                if (ev.Kind == StateEventKind.Destroyed)
                {
                    var occ = state.TickCalculations.GetDestroyedOccupant2(loc, state.grid);
                    if (occ != Occupant.None)
                    {
                        return (occ, ev.Completion.Progress());
                    }
                }

                return (state.grid.Get(loc), 0f);
            }
        }

        class BarrierDestructionAnimator : IDestructionAnimator
        {
            private int y;
            private int width;
            private Appointment animation;

            public void Reset(int y, int width, Appointment animation)
            {
                this.y = y;
                this.width = width;
                this.animation = animation;
            }

            // Closer to zero -> more pronounced ripple effect
            const float durationPerOcc = 0.2f;

            public (Occupant, float) GetDestroyedOccupant(Loc loc)
            {
                if (loc.Y == this.y)
                {
                    const float totalDelay = 1.0f - durationPerOcc;
                    float delayPerX = totalDelay / width;
                    float delay = loc.X * delayPerX;
                    float progress = animation.Progress();
                    float adjusted = Math.Max(0, progress - delay) / durationPerOcc;
                    return (Occupant.Barrier, adjusted);
                }

                return (Occupant.None, 0f);
            }
        }
    }
}
