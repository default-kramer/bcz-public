using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core
{
    public readonly struct Score
    {
        public readonly int ComboScore;
        public readonly int EnemyScore;
        public int TotalScore => ComboScore + EnemyScore;

        public Score(int combo, int enemy)
        {
            this.ComboScore = combo;
            this.EnemyScore = enemy;
        }

        public static Score operator +(Score a, Score b)
        {
            return new Score(a.ComboScore + b.ComboScore, a.EnemyScore + b.EnemyScore);
        }
    }
}
