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
        // WARNING - changing these names could break replays.
        // The values shouldn't matter, but why risk it? Just keep everything in the same order.
        Levels,
        ScoreAttack,
        PvPSim,
        [Obsolete("Didn't like it (barriers idea)")]
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

        int ScorePerEnemy { get; }

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
        public int ScorePerEnemy { get; set; } = 100;

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

        public static readonly ISettingsCollection BeginnerSettings = BeginnerSettingsCollection.Instance;
        public static readonly ISettingsCollection NormalSettings = NormalSettingsCollection.Instance;
        public static readonly ISettingsCollection PvPSimSettings = PvPSimSettingsCollection.Instance;
        public static readonly ISinglePlayerSettings ScoreAttackSettings = NormalSettingsCollection.ScoreAttack_Level14;
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
            public static readonly BeginnerSettingsCollection Instance = new();

            private BeginnerSettingsCollection() : base(20)
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
            const int NumLevels = 20;

            private static readonly IReadOnlyList<IReadOnlyList<IGoal>> goals;
            public static readonly NormalSettingsCollection Instance;
            public static readonly ISinglePlayerSettings ScoreAttack_Level14; // Just put the Score Attack settings here for now...

            static NormalSettingsCollection()
            {
                var goalsList = new List<IGoal>[NumLevels];

                for (int level = 1; level <= NumLevels; level++)
                {
                    // The increase starts at level 15.
                    // Gold goes from 500 at Level 14 up to 650 at Level 20.
                    int offset = Math.Max(0, level - 14);
                    goalsList[level - 1] = new List<IGoal>()
                    {
                        new MedalGoal(MedalKind.Bronze, 250 + offset * 25),
                        new MedalGoal(MedalKind.Silver, 350 + offset * 25),
                        new MedalGoal(MedalKind.Gold, 500 + offset * 25),
                    };
                }

                goals = goalsList;

                Instance = new NormalSettingsCollection();
                var level14 = Instance.GetSettings(14);
                var clone = new SinglePlayerSettings()
                {
                    // copy data:
                    EnemyCount = level14.EnemyCount,
                    EnemiesPerStripe = level14.EnemiesPerStripe,
                    RowsPerStripe = level14.RowsPerStripe,
                    GridHeight = level14.GridHeight,
                    GridWidth = level14.GridWidth,
                    SpawnBlanks = level14.SpawnBlanks,
                    // different data:
                    GameMode = GameMode.ScoreAttack,
                    ScorePerEnemy = 200,
                };
                ScoreAttack_Level14 = clone;
            }

            private NormalSettingsCollection() : base(NumLevels)
            {
                AddAll();
            }

            protected override bool SpawnBlanks(int level)
            {
                return true;
            }

            public override IReadOnlyList<IGoal> GetGoals(int level)
            {
                return goals[level - 1];
            }
        }

        class PvPSimSettingsCollection : SettingsCollection
        {
            public static readonly PvPSimSettingsCollection Instance = new();

            private PvPSimSettingsCollection() : base(20)
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
    }
}
