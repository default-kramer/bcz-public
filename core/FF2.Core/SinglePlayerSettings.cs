using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
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

        /// <summary>
        /// Right now, I'm not concerned about health and penalties during replays, I just want
        /// to be able to extract combos. So replaying games with no health or penalties
        /// allows me to re-tune the game while keeping all my old replays valid.
        /// This setting should eventually go away...
        /// </summary>
        bool InfiniteHealth { get; }

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
        public bool InfiniteHealth { get; set; } = false;

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

        abstract class SettingsCollection : ISettingsCollection
        {
            protected readonly ISinglePlayerSettings[] array;

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

            protected abstract bool SpawnBlanks(int level);

            protected void Add(int perStripe, int expectedHeight, int Level)
            {
                var settings = new SinglePlayerSettings()
                {
                    EnemyCount = Level * 4,
                    EnemiesPerStripe = perStripe,
                    RowsPerStripe = 2,
                    SpawnBlanks = SpawnBlanks(Level),
                };

                if (settings.CalculateEnemyHeight() != expectedHeight)
                {
                    throw new Exception($"Assert failed: {Level}, {expectedHeight}, {settings.CalculateEnemyHeight()}");
                }

                array[Level - 1] = settings;
            }

            private int LevelCounter;

            private void Add(int a, int b)
            {
                if (LevelCounter <= array.Length)
                {
                    Add(a, b, LevelCounter);
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
        }
    }
}
