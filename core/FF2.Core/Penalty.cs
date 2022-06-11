using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core
{
    public enum PenaltyKind
    {
        None = 0,
        Permanent = 1,
        Enemy = 2,
        Levelled = 3,
    }

    public readonly struct Penalty
    {
        public readonly PenaltyKind Kind;
        public readonly int Level;

        public Penalty(PenaltyKind kind, int level)
        {
            this.Kind = kind;
            this.Level = level;
        }
    }
}
