using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    readonly struct Combo
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

        public Combo AfterDestruction(TickCalculations calculations)
        {
            int v = NumVerticalGroups + calculations.NumVerticalGroups;
            int h = NumHorizontalGroups + calculations.NumHorizontalGroups;
            return new Combo(v, h);
        }
    }
}
