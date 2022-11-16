using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    /// <summary>
    /// Instead of creating new occupants, the health grid will redefine the meaning of existing occupants.
    /// </summary>
    public static class HealthOccupants
    {
        public static readonly Occupant Penalty = Occupant.IndestructibleEnemy;
        public static readonly Occupant Attack = Occupant.MakeCatalyst(Color.Blank, Direction.None);
        public static readonly Occupant Heart = Occupant.MakeCatalyst(Color.Red, Direction.None);
        public static readonly Occupant Heart0 = Occupant.MakeCatalyst(Color.Yellow, Direction.None);
        public static readonly Occupant Heart25 = Occupant.MakeCatalyst(Color.Yellow, Direction.Up);
        public static readonly Occupant Heart50 = Occupant.MakeCatalyst(Color.Yellow, Direction.Right);
        public static readonly Occupant Heart75 = Occupant.MakeCatalyst(Color.Yellow, Direction.Down);
        public static readonly Occupant Heart100 = Occupant.MakeCatalyst(Color.Yellow, Direction.Left);

        public static SpriteKind Translate(Occupant occ, SpriteKind elseValue)
        {
            if (occ == Attack) { return SpriteKind.Heartbreaker; }
            if (occ == Heart) { return SpriteKind.Heart; }
            if (occ == Heart0) { return SpriteKind.Heart0; }
            if (occ == Heart25) { return SpriteKind.Heart25; }
            if (occ == Heart50) { return SpriteKind.Heart50; }
            if (occ == Heart75) { return SpriteKind.Heart75; }
            if (occ == Heart100) { return SpriteKind.Heart100; }
            return elseValue;
        }
    }
}
