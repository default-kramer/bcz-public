using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    sealed class CountdownHook : EmptyStateHook, ICountdownViewmodel
    {
        private const int maxMillis = 1000 * 60;
        private const int millisRestoredPerEnemy = 1000 * 5;
        private Appointment countdown;

        public CountdownHook(IScheduler scheduler)
        {
            countdown = scheduler.CreateWaitingAppointment(maxMillis);
        }

        public int MaxMillis => maxMillis;
        public int CurrentMillis => countdown.MillisRemaining();

        public override bool GameOver => countdown.HasArrived();

        public override void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
        }
    }
}
