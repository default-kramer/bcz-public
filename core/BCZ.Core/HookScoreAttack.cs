using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    sealed class HookScoreAttack : EmptyStateHook
    {
        private const int gameDurationMillis = 1000 * 60 * 1; // 1 minute for now
        private readonly IStateData data;
        private readonly Appointment countdown;
        private bool isGameOver = false;

        public HookScoreAttack(IStateData data, IScheduler scheduler)
        {
            this.data = data;
            this.countdown = scheduler.CreateAppointment(gameDurationMillis);
        }

        public ICountdownViewmodel BuildCountdownVM(ITimer timer, Grid grid, ref IStateHook hook)
        {
            var vm = new CountdownVM(timer, grid, data);
            hook = new CompositeHook(hook, vm);
            return vm;
        }

        public override bool GameOver => isGameOver || CheckGameOver();

        private bool CheckGameOver()
        {
            if (countdown.HasArrived() && State.IsWaitingState(data.CurrentEvent.Kind))
            {
                isGameOver = true;
            }
            return isGameOver;
        }

        public override bool WillAddEnemies() => true;

        private class CountdownVM : CountdownViewmodel
        {
            public CountdownVM(ITimer timer, Grid grid, IStateData data) : base(timer, grid, data) { }

            public override int MaxMillis => gameDurationMillis;

            public override int CurrentMillis => gameDurationMillis - timer.Now.Millis;
        }
    }
}
