using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// Defines the rotation and x-translation that can be applied to a <see cref="Mover"/>
    /// before dropping it. The values of this struct are somewhat arbitrary; they are
    /// defined by <see cref="Mover.Orientation"/>.
    /// </summary>
    public readonly struct Orientation
    {
        public readonly Direction Direction;
        public readonly int X;

        public Orientation(Direction direction, int x)
        {
            this.Direction = direction;
            this.X = x;
        }
    }
}
