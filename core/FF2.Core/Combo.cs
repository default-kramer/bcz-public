using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public readonly struct ComboInfo
    {
        public readonly Combo StrictCombo;
        public readonly Combo PermissiveCombo;

        private ComboInfo(Combo strict, Combo permissive)
        {
            this.StrictCombo = strict;
            this.PermissiveCombo = permissive;
        }

        public static readonly ComboInfo Empty = new ComboInfo(Combo.Empty, Combo.Empty);

        internal ComboInfo AfterDestruction(GTickCalculations calculations)
        {
            var s2 = StrictCombo.AfterDestruction(calculations.NumVerticalGroupsStrict, calculations.NumHorizontalGroupsStrict);
            var p2 = PermissiveCombo.AfterDestruction(calculations.NumVerticalGroupsLoose, calculations.NumHorizontalGroupsLoose);
            return new ComboInfo(s2, p2);
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
    }
}
