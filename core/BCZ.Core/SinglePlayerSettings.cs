using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace BCZ.Core
{
    public enum GameMode
    {
        // WARNING - changing these names could break replays
        Levels,
        ScoreAttack,
        PvPSim,
        Levels2,
    }

    public readonly struct SeededSettings
    {
        public readonly PRNG.State Seed;
        public readonly ISinglePlayerSettings Settings;

        public SeededSettings(PRNG.State seed, ISinglePlayerSettings settings)
        {
            this.Seed = seed;
            this.Settings = settings;
        }
    }

    public interface ISinglePlayerSettings
    {
        int EnemyCount { get; }
        bool SpawnBlanks { get; }
        int GridWidth { get; }
        int GridHeight { get; }

        /// <summary>
        /// During enemy placement: Number of enemies per N rows, where N is <see cref="RowsPerStripe"/>.
        /// </summary>
        int EnemiesPerStripe { get; }

        /// <summary>
        /// See <see cref="EnemiesPerStripe"/>
        /// </summary>
        int RowsPerStripe { get; }

        GameMode GameMode { get; }

        IReadOnlyList<BarrierDefinition> Barriers { get; }

        SeededSettings AddRandomSeed();
    }

    public sealed class SinglePlayerSettings : ISinglePlayerSettings
    {
        public int EnemyCount { get; set; } = 15;
        public bool SpawnBlanks { get; set; } = true;
        public int GridWidth { get; set; } = Grid.DefaultWidth;
        public int GridHeight { get; set; } = Grid.DefaultHeight;
        public int EnemiesPerStripe { get; set; } = 5;
        public int RowsPerStripe { get; set; } = 2;
        public GameMode GameMode { get; set; } = GameMode.Levels;
        public IReadOnlyList<BarrierDefinition> Barriers => barriers;

        private readonly List<BarrierDefinition> barriers = new();

        public SinglePlayerSettings AddBarrier(int y, params int[] ranks)
        {
            barriers.Add(new BarrierDefinition(y, ranks));
            return this;
        }

        public static readonly SinglePlayerSettings Default = new SinglePlayerSettings();

        public SeededSettings AddRandomSeed()
        {
            return new SeededSettings(PRNG.RandomSeed(), this);
        }

        public int CalculateEnemyHeight()
        {
            int stripeCount = Math.DivRem(EnemyCount, EnemiesPerStripe, out int remainder);
            if (remainder > 0)
            {
                stripeCount++;
            }
            return stripeCount * RowsPerStripe;
        }

        public static readonly ISettingsCollection BeginnerSettings = new BeginnerSettingsCollection();
        public static readonly ISettingsCollection NormalSettings = new NormalSettingsCollection();
        public static readonly ISettingsCollection PvPSimSettings = new PvPSimSettingsCollection();
        public static readonly ISettingsCollection WIP = new TODO();
        private static readonly IReadOnlyList<IGoal> NoGoals = new List<IGoal>();

        abstract class SettingsCollection : ISettingsCollection
        {
            protected readonly ISinglePlayerSettings[] array;

            protected virtual GameMode GameMode => GameMode.Levels;

            public SettingsCollection(int maxLevel)
            {
                this.array = new ISinglePlayerSettings[maxLevel];
            }

            public int MaxLevel => array.Length;

            public ISinglePlayerSettings GetSettings(int level)
            {
                if (level < 1 || level > MaxLevel)
                {
                    throw new ArgumentOutOfRangeException(nameof(level));
                }
                return array[level - 1];
            }

            protected virtual bool SpawnBlanks(int level) => true;

            protected SinglePlayerSettings Add(int perStripe, int expectedHeight, int Level, int? enemyCount = null, int rowsPerStripe = 2)
            {
                var settings = new SinglePlayerSettings()
                {
                    EnemyCount = enemyCount ?? (Level * 4),
                    EnemiesPerStripe = perStripe,
                    RowsPerStripe = rowsPerStripe,
                    SpawnBlanks = SpawnBlanks(Level),
                    GameMode = this.GameMode,
                };

                if (settings.CalculateEnemyHeight() != expectedHeight)
                {
                    throw new Exception($"Assert failed: {Level}, {expectedHeight}, {settings.CalculateEnemyHeight()}");
                }

                array[Level - 1] = settings;
                return settings;
            }

            private int LevelCounter;

            private void Add(int perStripe, int expectedHeight)
            {
                if (LevelCounter <= array.Length)
                {
                    Add(perStripe, expectedHeight, LevelCounter);
                }
                LevelCounter++;
            }

            protected void AddAll()
            {
                LevelCounter = 1;
                Add(2, 4);
                Add(3, 6);
                Add(4, 6);
                Add(5, 8);
                Add(5, 8);
                Add(5, 10);
                Add(6, 10);
                Add(6, 12);
                Add(7, 12);
                Add(7, 12);
                Add(7, 14);
                Add(7, 14);
                Add(8, 14);
                Add(8, 14);
                Add(8, 16);
                Add(8, 16);
                Add(9, 16);
                Add(9, 16);
                Add(10, 16);
                Add(10, 16);

                if (array[array.Length - 1] == null)
                {
                    throw new Exception("Not enough levels are specified here");
                }
            }

            public abstract IReadOnlyList<IGoal> GetGoals(int level);
        }

        class BeginnerSettingsCollection : SettingsCollection
        {
            public BeginnerSettingsCollection() : base(20)
            {
                AddAll();
            }

            protected override bool SpawnBlanks(int level)
            {
                return false;
            }

            public override IReadOnlyList<IGoal> GetGoals(int level)
            {
                return NoGoals;
            }
        }

        class NormalSettingsCollection : SettingsCollection
        {
            public NormalSettingsCollection() : base(20)
            {
                AddAll();
            }

            protected override bool SpawnBlanks(int level)
            {
                return true;
            }

            public override IReadOnlyList<IGoal> GetGoals(int level)
            {
                return goals;
            }

            private static List<IGoal> goals = new List<IGoal>()
            {
                // TODO should be per level.
                // Gold seems like it should be about:
                // * Level 14 : 500
                // * Level 15 : 525
                // * Level 16 : 550
                // * ... +25 per level ...
                // * Level 20 : 650
                new FixedGoal(GoalKind.Bronze, 400),
                new FixedGoal(GoalKind.Silver, 500),
                new FixedGoal(GoalKind.Gold, 650),
            };
        }

        class PvPSimSettingsCollection : SettingsCollection
        {
            public PvPSimSettingsCollection() : base(20)
            {
                AddAll();
            }

            protected override bool SpawnBlanks(int level)
            {
                return true;
            }

            protected override GameMode GameMode => GameMode.PvPSim;

            public override IReadOnlyList<IGoal> GetGoals(int level)
            {
                return NoGoals;
            }
        }

        class TODO : SettingsCollection
        {
            protected override GameMode GameMode => GameMode.Levels2;

            public TODO() : base(12)
            {
                MyAdd(1);
                MyAdd(2);
                MyAdd(3);
                MyAdd(4).AddBarrier(2, 2);
                MyAdd(5).AddBarrier(2, 3);
                MyAdd(6).AddBarrier(2, 2, 3);
                MyAdd(7)
                    .AddBarrier(2, 4)
                    .AddBarrier(6, 2);
                MyAdd(8)
                    .AddBarrier(2, 3, 4)
                    .AddBarrier(6, 2, 3);
                MyAdd(9)
                    .AddBarrier(2, 3, 3, 5)
                    .AddBarrier(6, 2, 2, 3);
                MyAdd(10)
                    .AddBarrier(2, 4, 5, 6)
                    .AddBarrier(7, 2, 3, 4);
                MyAdd(11)
                    .AddBarrier(2, 3, 3, 5, 6)
                    .AddBarrier(10, 3, 3, 4);
                MyAdd(12)
                    .AddBarrier(2, 4, 5, 6, 7)
                    .AddBarrier(10, 2, 3, 3, 4);
            }

            private SinglePlayerSettings MyAdd(int level)
            {
                const int enemiesPerStripe = 5;
                return Add(enemiesPerStripe, level, level, enemyCount: level * 5, rowsPerStripe: 1);
            }

            public override IReadOnlyList<IGoal> GetGoals(int level)
            {
                return NoGoals;
            }
        }
    }
}
