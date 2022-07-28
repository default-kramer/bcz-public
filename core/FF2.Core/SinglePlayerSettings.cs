using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public interface ISinglePlayerSettings
    {
        PRNG.State PrngSeed { get; }
        int EnemyCount { get; }
        bool SpawnBlanks { get; }
        int GridWidth { get; }
        int GridHeight { get; }

        /// <summary>
        /// During enemy placement: Number of enemies per <see cref="RowsPerStripe"/> rows.
        /// </summary>
        int EnemiesPerStripe { get; }

        /// <summary>
        /// See <see cref="EnemiesPerStripe"/>
        /// </summary>
        int RowsPerStripe { get; }
    }

    public sealed class SinglePlayerSettings : ISinglePlayerSettings
    {
        public PRNG.State PrngSeed { get; set; }
        public int EnemyCount { get; set; } = 15;
        public bool SpawnBlanks { get; set; } = true;
        public int GridWidth { get; set; } = Grid.DefaultWidth;
        public int GridHeight { get; set; } = Grid.DefaultHeight;
        public int EnemiesPerStripe { get; set; } = 5;
        public int RowsPerStripe { get; set; } = 2;

        public static readonly SinglePlayerSettings Default = new SinglePlayerSettings().SetRandomSeed();

        /// <summary>
        /// TODO store seed on a different object to improve reusability?
        /// (Because each time you play level N, it is the only member that will change.)
        /// </summary>
        public SinglePlayerSettings SetRandomSeed()
        {
            PrngSeed = PRNG.RandomSeed();
            return this;
        }

        public const int MaxLevel = 40;
        public static readonly SinglePlayerSettings[] NormalModeSettingsPerLevel;

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
                settings.SetRandomSeed();
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
                settings.SetRandomSeed();
                NormalModeSettingsPerLevel[index] = settings;
            }
        }
    }
}
