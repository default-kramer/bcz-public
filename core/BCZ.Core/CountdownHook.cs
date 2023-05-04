using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    /// <summary>
    /// Refactoring likely to be needed: this class was just the countdown+gameover functionality,
    /// but now it is also implementing ICountdownViewmodel.
    /// Should separate the essential functionality from the viewmodel.
    /// </summary>
    sealed class CountdownHook : EmptyStateHook, ICountdownViewmodel
    {
        private const int maxMillis = 1000 * 60;
        private const int millisRestoredPerEnemy = 1000 * 5;
        private Appointment countdown;
        private readonly IStateData data;
        private readonly Grid grid;
        private readonly ITimer timer;

        public CountdownHook(IScheduler scheduler, IStateData data, Grid grid, ITimer timer)
        {
            countdown = scheduler.CreateWaitingAppointment(maxMillis);
            this.data = data;
            this.grid = grid;
            this.timer = timer;
        }

        public int MaxMillis => maxMillis;
        public int CurrentMillis => countdown.MillisRemaining();

        public override bool GameOver => countdown.HasArrived();

        public TimeSpan Time => timer.Now.ToTimeSpan();

        public (Combo, int score) LastCombo { get; private set; } = (Combo.Empty, 0);

        public int Score => data.Score;

        public int EnemiesRemaining(Color color)
        {
            return grid.Stats.EnemiesRemaining(color);
        }

        public override void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler, int score)
        {
            LastCombo = (current.ComboToReward, score);
        }

        public override void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
        }
    }
}
