using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public readonly struct DeckItem
    {
        public readonly Color LeftColor;
        public readonly Color RightColor;

        public DeckItem(Color left, Color right)
        {
            this.LeftColor = left;
            this.RightColor = right;
        }
    }
}
