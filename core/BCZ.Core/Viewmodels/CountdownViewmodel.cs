using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BCZ.Core.Viewmodels
{
    abstract class CountdownViewmodel : EmptyStateHook, ICountdownViewmodel
    {
        protected readonly ITimer timer;
        private readonly Grid grid;
        private readonly IStateData data;

        public CountdownViewmodel(ITimer timer, Grid grid, IStateData data)
        {
            this.timer = timer;
            this.grid = grid;
            this.data = data;
        }

        public abstract int MaxMillis { get; }

        public abstract int CurrentMillis { get; }

        public TimeSpan Time => timer.Now.ToTimeSpan();

        public int Score => data.Score;

        public (Combo, int score) LastCombo { get; private set; } = (Combo.Empty, 0);

        public int EnemiesRemaining(Color color)
        {
            return grid.Stats.EnemiesRemaining(color);
        }

        public override void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler, int score)
        {
            LastCombo = (current.ComboToReward, score);
        }
    }
}
