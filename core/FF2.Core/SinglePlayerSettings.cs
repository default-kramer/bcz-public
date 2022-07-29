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

        public static readonly SinglePlayerSettings Default = new SinglePlayerSettings();

        public const int MaxLevel = 40;
        public static readonly ISinglePlayerSettings[] NormalModeSettingsPerLevel;

        static SinglePlayerSettings()
        {
            NormalModeSettingsPerLevel = new SinglePlayerSettings[MaxLevel];
            int level = 0;

            // Levels 1-9 : no blanks
            while (level < 9)
            {
                int index = level;
                level++;

                var settings = new SinglePlayerSettings()
                {
                    EnemyCount = level * 2 + 4,
                    SpawnBlanks = false,
                };
                NormalModeSettingsPerLevel[index] = settings;
            }

            // Levels 10-40 : blanks
            const int offset = 10;
            while (level < MaxLevel)
            {
                int index = level;
                level++;

                int density = (level - offset) / 4 + 5;

                var settings = new SinglePlayerSettings()
                {
                    EnemyCount = (level - offset) * 2 + 10,
                    SpawnBlanks = true,
                    EnemiesPerStripe = density,
                    RowsPerStripe = 2,
                };
                NormalModeSettingsPerLevel[index] = settings;
            }
        }

        public SeededSettings AddRandomSeed()
        {
            return new SeededSettings(PRNG.RandomSeed(), this);
        }
    }
}
