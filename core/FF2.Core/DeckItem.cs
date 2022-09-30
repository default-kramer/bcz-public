using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public readonly struct SpawnItem
    {
        public readonly Color LeftColor;
        public readonly Color RightColor;

        public SpawnItem(Color left, Color right)
        {
            this.LeftColor = left;
            this.RightColor = right;
        }
    }
}
