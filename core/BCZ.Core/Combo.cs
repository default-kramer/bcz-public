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
            this.PermissiveCombo = permissive.ApplyDeductions(strict);
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

        /// <summary>
        /// This number will usually be zero. It will never be negative.
        /// A positive number indicates how much the <see cref="Rank"/> must be reduced
        /// in order to respect the <see cref="PermissiveLimit"/>.
        /// </summary>
        public readonly int Deductions;

        /// <summary>
        /// We usually reward the permissive combo.
        /// To discourage spamming combos that contain relatively few enemies, we enforce that the <see cref="Rank"/>
        /// of the permissive combo cannot exceed that of the strict combo by more than this number.
        /// </summary>
        const int PermissiveLimit = 2;

        public Combo()
        {
            NumVerticalGroups = 0;
            NumHorizontalGroups = 0;
            Deductions = 0;
        }

        private Combo(int numVerticalGroups, int numHorizontalGroups, int deductions)
        {
            this.NumVerticalGroups = numVerticalGroups;
            this.NumHorizontalGroups = numHorizontalGroups;
            this.Deductions = deductions;
        }

        public static readonly Combo Empty = new Combo();

        public int AdjustedGroupCount
        {
            get { return NumVerticalGroups + NumHorizontalGroups * 2; }
        }

        public int Rank => AdjustedGroupCount - Deductions;

        public Combo AfterDestruction(int numVerticalGroups, int numHorizontalGroups)
        {
            return new Combo(this.NumVerticalGroups + numVerticalGroups, this.NumHorizontalGroups + numHorizontalGroups, 0);
        }

        /// <summary>
        /// Assumes that this combo is the permissive combo.
        /// </summary>
        public Combo ApplyDeductions(Combo strictCombo)
        {
            int excess = this.AdjustedGroupCount - strictCombo.AdjustedGroupCount;
            int deductions = Math.Max(0, excess - PermissiveLimit);
            return new Combo(this.NumVerticalGroups, this.NumHorizontalGroups, deductions);
        }

        public string Describe(string ifZero)
        {
            if (AdjustedGroupCount <= 0)
            {
                return ifZero;
            }
            return $"{Numeral} ({Description()})";
        }

        public string Numeral
        {
            get
            {
                int index = Math.Min(Rank, Numerals.Length - 1);
                return Numerals[index];
            }
        }

        public string Description()
        {
            if (Deductions > 0)
            {
                return $"{NumVerticalGroups}v{NumHorizontalGroups}h-{Deductions}";
            }
            else
            {
                return $"{NumVerticalGroups}v{NumHorizontalGroups}h";
            }
        }

        private static readonly string[] Numerals = new[] { "ZERO", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XII", "XIV", "XV", "XVI" };
    }
}
