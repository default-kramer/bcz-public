using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.Viewmodels
{
    public interface ICountdownViewmodel
    {
        int MaxMillis { get; }

        int CurrentMillis { get; }

        public TimeSpan Time { get; }

        public int EnemiesRemaining(Color color);

        public (Combo, int score) LastCombo { get; }

        public int Score { get; }
    }
}
