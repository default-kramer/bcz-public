using System;
using System.Collections.Generic;
using System.Linq;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    sealed class HookLevelsMode : EmptyStateHook
    {
        private const int maxMillis = 1000 * 60;
        private const int millisRestoredPerEnemy = 1000 * 5;
        private Appointment countdown;

        public HookLevelsMode(IScheduler scheduler)
        {
            countdown = scheduler.CreateWaitingAppointment(maxMillis);
        }

        public ICountdownViewmodel BuildCountdownVM(ITimer timer, Grid grid, IStateData data, ref IStateHook hook)
        {
            var vm = new CountdownVM(timer, grid, data, this);
            hook = new CompositeHook(hook, vm);
            return vm;
        }

        public override bool GameOver => countdown.HasArrived();

        public override void OnComboLikelyCompleted(State state, ComboInfo combo, IScheduler scheduler)
        {
            int millisRemaining = countdown.MillisRemaining();
            millisRemaining += combo.NumEnemiesDestroyed * millisRestoredPerEnemy;
            millisRemaining = Math.Min(millisRemaining, maxMillis);
            countdown = scheduler.CreateWaitingAppointment(millisRemaining);
        }

        private class CountdownVM : CountdownViewmodel
        {
            private readonly HookLevelsMode hook;

            public CountdownVM(ITimer timer, Grid grid, IStateData data, HookLevelsMode hook) : base(timer, grid, data)
            {
                this.hook = hook;
            }

            public override int MaxMillis => maxMillis;
            public override int CurrentMillis => hook.countdown.MillisRemaining();
        }
    }
}
