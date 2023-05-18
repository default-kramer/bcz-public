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

        /// <summary>
        /// How many millis remain until the game ends?
        /// Used to draw the bar gauge.
        /// </summary>
        int RemainingMillis { get; }

        /// <summary>
        /// The time to show (can be elapsed or remaining).
        /// Displays as "0:42" for example.
        /// </summary>
        public TimeSpan Time { get; }

        public int EnemiesRemaining(Color color);

        public (Combo, int score) LastCombo { get; }

        public int Score { get; }
    }
}
