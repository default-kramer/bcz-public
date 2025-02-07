using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    /// <summary>
    /// A read-only view of some mutable state data
    /// </summary>
    public interface IStateData
    {
        Score Score { get; }
        StateEvent CurrentEvent { get; }
        Moment LastComboMoment { get; }

        int EfficiencyInt();
    }

    public sealed class State : IDumpCallback
    {
        private readonly Grid grid;
        private readonly FallAnimationSampler fallSampler;
        private readonly ISpawnDeck spawnDeck;
        private Mover? mover;
        public ComboInfo BestCombo { get; private set; } = ComboInfo.Empty;
        private ComboInfo currentCombo;
        public ComboInfo? ActiveOrPreviousCombo { get; private set; } = null;
        private readonly PayoutTable scorePayoutTable = PayoutTable.DefaultScorePayoutTable;
        private readonly int scorePerEnemy;
        private readonly IStateHook hook;
        private readonly StateEvent.Factory eventFactory;
        private readonly Timekeeper timekeeper;
        private readonly IScheduler scheduler;
        private readonly DumpAnimator dumpAnimator;
        private readonly FallAnimator fallAnimator;
        private readonly IDestructionAnimator destructionAnimator;
        private readonly BarrierDestructionAnimator barrierDestructionAnimator;
        private readonly StateData stateData;
        public StateEvent CurrentEvent => stateData.currentEvent;

        public Score Score => stateData.Score;
        public int NumCombos => stateData.NumCombos;

        public int NumCatalystsSpawned = 0; // Try not to use this...

        public IStateData Data => stateData;

        public static bool IsWaitingState(StateEventKind kind) => kind == StateEventKind.Spawned;

        public bool IsWaitingOnUser => IsWaitingState(CurrentEvent.Kind);

        public bool IsGameOver => CurrentEvent.Kind == StateEventKind.GameEnded;

        /// <summary>
        /// If the player <see cref="ClearedAllEnemies"/>, this will hold the moment when that
        /// happened. Otherwise it will be the moment that we transitioned to <see cref="StateEventKind.GameEnded"/>.
        /// </summary>
        public Moment? FinishTime { get; private set; }

        /// <summary>
        /// When the player clears the final enemy, we set this to true immediately
        /// but we don't transition to <see cref="StateEventKind.GameEnded"/> until the combo resolves.
        /// </summary>
        public bool ClearedAllEnemies { get; private set; }

        public delegate void EventHandler<T>(State sender, T args);

        // Not sure how I feel about using events here...
        // Be careful to avoid subscribing to something that will live longer than you expect.
        public event EventHandler<ComboInfo>? OnComboLikelyCompleted;
        public event EventHandler<SpawnItem>? OnCatalystSpawned;

        public static State CreateWithInfiniteHealth(Grid grid, ISpawnDeck deck)
        {
            const int scorePerEnemy = 100;
            return new State(grid, deck, NullStateHook.Instance, new Timekeeper(), new StateData(), null, null, null, scorePerEnemy);
        }

        private State(Grid grid, ISpawnDeck spawnDeck, IStateHook hook, Timekeeper timekeeper, StateData stateData,
            IAttackGridViewmodel? attackGridViewmodel,
            ISwitchesViewmodel? switchesViewmodel,
            ICountdownViewmodel? countdownViewmodel,
            int scorePerEnemy)
        {
            this.grid = grid;
            this.fallSampler = new FallAnimationSampler(grid);
            this.spawnDeck = spawnDeck;
            this.eventFactory = new();
            this.timekeeper = timekeeper;
            this.scheduler = timekeeper;
            this.stateData = stateData;
            dumpAnimator = new DumpAnimator(grid.Width, grid.Height + 2); // dump from the mover area
            fallAnimator = new FallAnimator(fallSampler, 0f);
            destructionAnimator = new StandardDestructionAnimator(this);
            barrierDestructionAnimator = new BarrierDestructionAnimator();
            mover = null;
            currentCombo = ComboInfo.Empty;
            this.hook = hook;
            this.AttackGridViewmodel = attackGridViewmodel;
            this.SwitchesViewmodel = switchesViewmodel;
            this.CountdownViewmodel = countdownViewmodel;
            this.scorePerEnemy = scorePerEnemy;
        }

        public readonly ICountdownViewmodel? CountdownViewmodel;
        public readonly IAttackGridViewmodel? AttackGridViewmodel;
        public readonly ISwitchesViewmodel? SwitchesViewmodel;

        public IReadOnlyGrid Grid { get { return grid; } }

        public ITickCalculations TickCalculations => grid.TickCalc;

        public static State Create(SeededSettings ss)
        {
            var settings = ss.Settings;
            var spawns = ss.Settings.SpawnBlanks ? Lists.MainDeck : Lists.BlanklessDeck;
            var deck = new InfiniteSpawnDeck(spawns, new PRNG(ss.Seed));
            var timekeeper = new Timekeeper();
            var stateData = new StateData();

            var mode = ss.Settings.GameMode;
            if (mode == GameMode.PvPSim)
            {
                var grid = Core.Grid.Create(ss.Settings, new PRNG(ss.Seed));
                var switches = new Switches();
                var hook = new SimulatedAttacker(switches);
                return new State(grid, deck, hook, timekeeper, stateData, hook.VM, hook.SwitchVM, null, settings.ScorePerEnemy);
            }
            else if (mode == GameMode.Levels)
            {
                var grid = Core.Grid.Create(ss.Settings, new PRNG(ss.Seed));
                var levelsHook = new HookLevelsMode(timekeeper);
                IStateHook hook = levelsHook;
                var countdownVM = levelsHook.BuildCountdownVM(timekeeper, grid, stateData, ref hook);
                return new State(grid, deck, hook, timekeeper, stateData, null, null, countdownVM, settings.ScorePerEnemy);
            }
            else if (mode == GameMode.ScoreAttack)
            {
                var scoreAttackHook = new HookScoreAttackTall(stateData, timekeeper, ss, out var grid);
                IStateHook hook = scoreAttackHook;
                var countdownVM = scoreAttackHook.BuildCountdownVM(timekeeper, ref hook);
                return new State(grid, deck, hook, timekeeper, stateData, null, null, countdownVM, settings.ScorePerEnemy);
            }
            else if (mode == GameMode.ScoreAttackWide)
            {
                var scoreAttackHook = new HookScoreAttackWide(stateData, timekeeper, ss, out var grid);
                IStateHook hook = scoreAttackHook;
                var countdownVM = scoreAttackHook.BuildCountdownVM(timekeeper, ref hook);
                return new State(grid, deck, hook, timekeeper, stateData, null, null, countdownVM, settings.ScorePerEnemy);
            }
            else
            {
                throw new Exception("Unsupported game mode: " + mode);
            }
        }

        public Viewmodels.QueueModel MakeQueueModel()
        {
            return new Viewmodels.QueueModel(this.spawnDeck);
        }

        // TODO this should probably go away ...
        public void Elapse(Moment now)
        {
            if (CurrentEvent.Kind == StateEventKind.GameEnded)
            {
                return;
            }
            timekeeper.Elapse(now, this);
        }

        // ... and this should become non-TEMP code.
        internal void TEMP_TimekeeperHook(Moment now)
        {
            Transition(now);

            if (hook.GameOver)
            {
                FinishTime = FinishTime ?? now;
                Update(StateEvent.GameEnded);
            }
        }

        public bool HandleCommand(Command command, Moment now)
        {
            if (CurrentEvent.Kind == StateEventKind.GameEnded)
            {
                return false;
            }

            Elapse(now);

            if (CurrentEvent.Kind == StateEventKind.GameEnded)
            {
                return false;
            }

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
            stateData.currentEvent = result.Value;
            return true;
        }

        [Obsolete("Attention call site: Your argument is no longer nullable. You should call Update() instead.")]
        private bool MaybeUpdate(StateEvent result)
        {
            throw new Exception("I told you not to call this...");
        }

        private bool Update(StateEvent result)
        {
            stateData.currentEvent = result;
            return true;
        }

        private StateEvent Spawn()
        {
            if (currentCombo.PermissiveCombo.AdjustedGroupCount > 0)
            {
                var scorePayout = GetHypotheticalScore(currentCombo);
                ITimer timer = timekeeper;
                stateData.OnComboCompleted(scorePayout, timer.Now);
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

            // It's possible that wide layout cleared one half, leaving an orphan on the other half which will now fall:
            if (Fall(out var fallEvent))
            {
                return fallEvent;
            }

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
                hook.OnComboUpdated(previous, newCombo, scheduler, GetHypotheticalScore(newCombo));
            }
            else
            {
                if (currentCombo.TotalNumGroups > 0)
                {
                    UpdateBestCombo(currentCombo);
                    hook.OnComboLikelyCompleted(this, currentCombo, scheduler);
                    OnComboLikelyCompleted?.Invoke(this, currentCombo);
                }
            }
            Slowmo = Slowmo || result;
            return result;
        }

        private static int ComboQuality(Combo combo)
        {
            // The actual value doesn't really matter here.
            // It's just a convenient way to implement tiebreakers.
            return 0
                + 10000 * combo.Rank
                + 100 * combo.AdjustedGroupCount
                + 1 * combo.NumHorizontalGroups;
        }

        private bool UpdateBestCombo(ComboInfo candidate)
        {
            var exist = BestCombo.ComboToReward;
            var cand = candidate.ComboToReward;
            if (ComboQuality(cand) <= ComboQuality(exist))
            {
                return false;
            }
            BestCombo = candidate;
            return true;
        }

        /// <summary>
        /// If the given <paramref name="combo"/> were played, how much would it score?
        /// </summary>
        public Score GetHypotheticalScore(ComboInfo combo)
        {
            int enemyScore = combo.NumEnemiesDestroyed * scorePerEnemy;
            // By adding a single point per deduction, it will be super-obvious at the end of the game.
            // For example, if your final score is 16,407 you most likely had 7 deductions throughout the game.
            int comboScore = scorePayoutTable.GetPayout(combo.ComboToReward.Rank) + combo.ComboToReward.Deductions;
            return new Score(comboScore, enemyScore);
        }

        private bool Transition(Moment now)
        {
            bool retval = __SingleTransition();
            while (__SingleTransition()) { }
            if (retval && grid.Stats.EnemyCount == 0 && !hook.WillAddEnemies())
            {
                // Don't change to GameOver immediately. Let the combo resolve.
                ClearedAllEnemies = true;
                FinishTime = now;
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

        private bool Fall(out StateEvent ev)
        {
            if (Fall(true))
            {
                int millis = Constants.FallingMillisPerCell * fallSampler.MaxFall();
                ev = eventFactory.Fell(fallSampler, scheduler.CreateAppointment(millis));
                return true;
            }

            ev = default;
            return false;
        }

        private StateEvent FallOrDestroyOrSpawn()
        {
            if (Fall(out var fallEvent))
            {
                return fallEvent;
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

        class StateData : IStateData
        {
            public Score Score { get; private set; }
            public StateEvent currentEvent = StateEvent.StateConstructed;
            public int NumCombos { get; private set; }
            public Moment LastComboMoment { get; private set; }

            public void OnComboCompleted(Score score, Moment now)
            {
                this.Score += score;
                this.NumCombos++;
                this.LastComboMoment = now;
            }

            StateEvent IStateData.CurrentEvent => currentEvent;

            int IStateData.EfficiencyInt()
            {
                var comboCount = NumCombos;
                if (comboCount == 0) return 0;
                return Score.TotalScore / comboCount;
            }
        }
    }
}
