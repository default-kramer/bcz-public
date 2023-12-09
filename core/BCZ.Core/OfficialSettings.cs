using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    public readonly struct OfficialSettingsId : IEquatable<OfficialSettingsId>
    {
        public readonly string Id;

        public OfficialSettingsId(string id)
        {
            this.Id = id;
        }

        public override string ToString() => Id; // make sure we never accidentally write something opaque

        public override int GetHashCode() => Id.GetHashCode();
        public bool Equals(OfficialSettingsId other) => Id == other.Id;
        public override bool Equals(object? obj)
        {
            return (obj is OfficialSettingsId other)
                && this.Equals(other);
        }

        /// <summary>
        /// Validates that this value is legitimate and provides the corresponding settings if so.
        /// </summary>
        public bool Validate(out ISinglePlayerSettings settings)
        {
            settings = Validate(this.Id)!;
            return settings != null;
        }

        private static ISinglePlayerSettings? Validate(string id)
        {
            switch (id)
            {
                case nameof(ScoreAttackV0):
                    return OfficialSettings.ScoreAttackV0;
                case nameof(ScoreAttackWide5):
                    return OfficialSettings.ScoreAttackWide5;
                default:
                    return null;
            }
        }

        // These names should never change once they get used
        public static readonly OfficialSettingsId ScoreAttackV0 = new(nameof(ScoreAttackV0));
        public static readonly OfficialSettingsId ScoreAttackWide5 = new(nameof(ScoreAttackWide5));
    }

    public static class OfficialSettings
    {
        public static readonly ISinglePlayerSettings ScoreAttackV0 = new SinglePlayerSettings()
        {
            OfficialSettingsId = OfficialSettingsId.ScoreAttackV0,
            EnemyCount = 56,
            SpawnBlanks = true,
            GridWidth = 8,
            GridHeight = 20,
            EnemiesPerStripe = 8,
            RowsPerStripe = 2,
            GameMode = GameMode.ScoreAttack,
            // TODO I upped this from 100 to 200 because I was worried about spamming empty combos.
            // Now that I have set an upper bound on "how permissive" we will be, I wonder if this
            // should go back to 100...?
            ScorePerEnemy = 200,
        };

        public static readonly ISinglePlayerSettings ScoreAttackWide5 = new SinglePlayerSettings()
        {
            OfficialSettingsId = OfficialSettingsId.ScoreAttackWide5,
            EnemyCount = -1, // N/A for Wide layouts
            SpawnBlanks = true,
            GridWidth = 16,
            GridHeight = 16,
            GameMode = GameMode.ScoreAttackWide,
            ScorePerEnemy = 200,
        };
    }
}
