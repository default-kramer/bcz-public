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
        private CorruptionManager corruption;
        private ComboInfo currentCombo;
        private readonly PenaltyManager penalties;
        private PenaltySchedule penaltySchedule;
        private int waitingMillis = 0;
        private Moment lastMoment = new Moment(0); // TODO should see about enforcing "cannot change Kind without providing a Moment"
        private int score = 0;
        private readonly PayoutTable scorePayoutTable = PayoutTable.DefaultScorePayoutTable;
        public readonly BLAH healthGrid; // TODO

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
            corruption = new CorruptionManager();
            currentCombo = ComboInfo.Empty;
            penalties = new PenaltyManager();
            penaltySchedule = PenaltySchedule.BasicSchedule(10 * 1000);
            healthGrid = new BLAH(this);
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

                corruption = corruption.Elapse(millis);

                if (corruption.Progress >= 1m)
                {
                    ChangeKind(true, StateKind.GameOver, StateKind.GameOver);
                    return;
                }

                if (penaltySchedule.TryAdvance(waitingMillis, out var nextPS))
                {
                    penalties.Add(penaltySchedule.Penalty);
                    penaltySchedule = nextPS;

                    corruption = corruption.OnPenaltiesChanged(penalties);
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

        public decimal CorruptionProgress { get { return corruption.Progress; } }

        public int RemainingMillis { get { return corruption.RemainingMillis; } }

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
                    var rewardCombo = currentCombo.ComboToReward;
                    var scorePayout = GetHypotheticalScore(currentCombo);
                    score += scorePayout;
                    //Console.WriteLine($"Score: {score} (+{scorePayout})");
                    corruption = corruption.OnComboCompleted(rewardCombo);
                    penalties.OnComboCompleted(rewardCombo);
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

    public sealed class BLAH : IReadOnlyGrid
    {
        private const int W = 4;
        private const int H = 12;

        // A heart should be shown at this index when the corruption is less than the threshold.
        // If there are 6 hearts, the values should be 1/6, 2/6, ... 6/6.
        // For cells that never show a heart, use a negative value.
        private static readonly decimal[] heartData;

        static BLAH()
        {
            heartData = new decimal[W * H];
            heartData.AsSpan().Fill(-999);

            var size = new GridSize(W, H);

            const int numHealth = 6;
            const int healthPerRow = 2;

            decimal healthPerStep = 1m / numHealth;
            for (int row = 0; row < numHealth / healthPerRow; row++)
            {
                for (int col = 0; col < healthPerRow; col++)
                {
                    var x = col * W / healthPerRow + row % 2;
                    var y = row * 2 + 1;
                    var loc = new Loc(x, y);
                    heartData[size.LocIndex(loc)] = healthPerStep * (row * healthPerRow + col + 1);
                }
            }
        }

        private readonly State state;

        public BLAH(State state)
        {
            this.state = state;
        }

        public int Width => W;

        public int Height => H;

        public GridSize Size => new GridSize(W, H);

        public string PrintGrid => throw new NotImplementedException();

        public string DiffGridString(params string[] rows)
        {
            throw new NotImplementedException();
        }

        public Occupant Get(Loc loc)
        {
            var index = loc.ToIndex(this);
            if (state.CorruptionProgress < heartData[index])
            {
                return Occupant.MakeEnemy(Color.Blank);
            }
            return Occupant.None;
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

        // TODO extension method:
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
