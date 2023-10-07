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
        private const int gameDurationMillis = 1000 * 60 * 5;
        private const float gameDurationMillisAsSingle = gameDurationMillis;
        private readonly IStateData data;
        private readonly ISinglePlayerSettings settings;
        private readonly Appointment countdown;
        private readonly PRNG prng; // This PRNG must not be shared. It is only for creating and re-creating the grid.
        private readonly Grid grid;
        private bool isGameOver = false;

        public HookScoreAttack(IStateData data, IScheduler scheduler, SeededSettings settings, out Grid grid)
        {
            this.data = data;
            this.settings = settings.Settings;
            this.countdown = scheduler.CreateAppointment(gameDurationMillis);
            this.prng = new PRNG(settings.Seed);
            this.grid = Grid.Create(this.settings, prng);
            grid = this.grid;
        }

        public ICountdownViewmodel BuildCountdownVM(ITimer timer, ref IStateHook hook)
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

        public override void PreSpawn(State state, int spawnCount)
        {
            if (grid.Stats.EnemyCount == 0)
            {
                grid.Clear();
                GridCreateHelper.SetupGrid(grid, prng, settings);
            }
        }

        private class CountdownVM : CountdownViewmodel
        {
            public CountdownVM(ITimer timer, Grid grid, IStateData data) : base(timer, grid, data) { }

            public override float MaxMillisAsSingle => gameDurationMillisAsSingle;

            public override int RemainingMillis => gameDurationMillis - timer.Now.Millis;

            public override TimeSpan Time => TimeSpan.FromMilliseconds(gameDurationMillis - timer.Now.Millis);
        }
    }
}
