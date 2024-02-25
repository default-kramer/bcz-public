using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core.Viewmodels;

namespace BCZ.Core
{
    abstract class BaseHookScoreAttack : EmptyStateHook
    {
        private const int gameDurationMillis = 1000 * 60 * 5;
        private const float gameDurationMillisAsSingle = gameDurationMillis;
        private readonly IStateData data;
        protected readonly ISinglePlayerSettings settings;
        private readonly Appointment countdown;
        protected readonly PRNG prng; // This PRNG must not be shared. It is only for creating and re-creating the grid.
        protected readonly Grid grid;
        private bool isGameOver = false;

        public BaseHookScoreAttack(IStateData data, IScheduler scheduler, SeededSettings settings, out Grid grid)
        {
            this.data = data;
            this.settings = settings.Settings;
            this.countdown = scheduler.CreateAppointment(gameDurationMillis);
            this.prng = new PRNG(settings.Seed);
            this.grid = CreateInitialGrid(this.settings, prng);
            grid = this.grid;
        }

        /// <summary>
        /// WARNING - subclasses should implement this method as if it were static - it will be called during the
        /// constructor and members may still be uninitialized.
        /// </summary>
        protected abstract Grid CreateInitialGrid(ISinglePlayerSettings settings, PRNG prng);

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

        private class CountdownVM : CountdownViewmodel
        {
            public CountdownVM(ITimer timer, Grid grid, IStateData data) : base(timer, grid, data) { }

            public override float MaxMillisAsSingle => gameDurationMillisAsSingle;

            public override int RemainingMillis => gameDurationMillis - timer.Now.Millis;

            public override TimeSpan Time => TimeSpan.FromMilliseconds(gameDurationMillis - timer.Now.Millis);
        }
    }

    sealed class HookScoreAttackTall : BaseHookScoreAttack
    {
        public HookScoreAttackTall(IStateData data, IScheduler scheduler, SeededSettings settings, out Grid grid)
            : base(data, scheduler, settings, out grid) { }

        protected override Grid CreateInitialGrid(ISinglePlayerSettings settings, PRNG prng)
        {
            return Grid.Create(settings, prng);
        }

        public override void PreSpawn(State state, int spawnCount)
        {
            if (grid.Stats.EnemyCount == 0)
            {
                grid.Clear();
                GridCreateHelper.SetupGrid(grid, prng, settings);
            }
        }
    }

    sealed class HookScoreAttackWide : BaseHookScoreAttack
    {
        public HookScoreAttackWide(IStateData data, IScheduler scheduler, SeededSettings settings, out Grid grid)
            : base(data, scheduler, settings, out grid) { }

        protected override Grid CreateInitialGrid(ISinglePlayerSettings settings, PRNG prng)
        {
            var grid = Grid.Create(settings.GridWidth, settings.GridHeight);
            WideLayoutHelper.MaybeRefillWideLayout(grid, prng);
            return grid;
        }

        public override void PreSpawn(State state, int spawnCount)
        {
            WideLayoutHelper.MaybeRefillWideLayout(grid, prng);
        }
    }
}
