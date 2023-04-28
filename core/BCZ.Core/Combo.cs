using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    public readonly struct ComboInfo
    {
        public readonly Combo StrictCombo;
        public readonly Combo PermissiveCombo;
        public readonly int NumEnemiesDestroyed;

        private ComboInfo(Combo strict, Combo permissive, int enemiesDestroyed)
        {
            this.StrictCombo = strict;
            this.PermissiveCombo = permissive;
            this.NumEnemiesDestroyed = enemiesDestroyed;
        }

        /// <summary>
        /// Still TBD if I want to use the Permissive combo for all rewards.
        /// If I ever change my mind, remove this property and review all broken call sites.
        /// </summary>
        public Combo ComboToReward => PermissiveCombo;

        public static readonly ComboInfo Empty = new ComboInfo(Combo.Empty, Combo.Empty, 0);

        public int TotalNumGroups => PermissiveCombo.NumVerticalGroups + PermissiveCombo.NumHorizontalGroups;

        /// <summary>
        /// This can get called multiple times per "destruction cycle"
        /// </summary>
        internal ComboInfo AfterDestruction(DestructionCalculations calculations)
        {
            var strict2 = StrictCombo.AfterDestruction(calculations.NumVerticalGroupsStrict, calculations.NumHorizontalGroupsStrict);
            var perm2 = PermissiveCombo.AfterDestruction(calculations.NumVerticalGroupsPermissive, calculations.NumHorizontalGroupsPermissive);
            return new ComboInfo(strict2, perm2, NumEnemiesDestroyed + calculations.NumEnemiesDestroyed);
        }

        /// <summary>
        /// How many destruction groups did not have at least one enemy?
        /// </summary>
        public int AllCatalystGroupCount =>
            PermissiveCombo.NumVerticalGroups + PermissiveCombo.NumHorizontalGroups
            - StrictCombo.NumVerticalGroups - StrictCombo.NumHorizontalGroups;
    }

    public readonly struct Combo
    {
        public readonly int NumVerticalGroups;
        public readonly int NumHorizontalGroups;

        public Combo()
        {
            NumVerticalGroups = 0;
            NumHorizontalGroups = 0;
        }

        private Combo(int numVerticalGroups, int numHorizontalGroups)
        {
            this.NumVerticalGroups = numVerticalGroups;
            this.NumHorizontalGroups = numHorizontalGroups;
        }

        public static readonly Combo Empty = new Combo();

        public int AdjustedGroupCount
        {
            get { return NumVerticalGroups + NumHorizontalGroups * 2; }
        }

        public Combo AfterDestruction(int numVerticalGroups, int numHorizontalGroups)
        {
            return new Combo(this.NumVerticalGroups + numVerticalGroups, this.NumHorizontalGroups + numHorizontalGroups);
        }

        public string Describe(string ifZero)
        {
            if (AdjustedGroupCount <= 0)
            {
                return ifZero;
            }
            int index = Math.Min(AdjustedGroupCount, Numerals.Length - 1);
            string numeral = Numerals[index];
            return $"{numeral} ({NumVerticalGroups}v{NumHorizontalGroups}h)";
        }

        private static readonly string[] Numerals = new[] { "ZERO", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XII", "XIV", "XV", "XVI" };
    }
}
