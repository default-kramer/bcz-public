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

        public abstract float MaxMillisAsSingle { get; }

        public abstract int RemainingMillis { get; }

        public virtual TimeSpan Time => timer.Now.ToTimeSpan();

        public int Score => data.Score.TotalScore;

        public (Combo, int score) LastCombo { get; private set; } = (Combo.Empty, 0);

        public int EnemiesRemaining(Color color)
        {
            return grid.Stats.EnemiesRemaining(color);
        }

        public override void OnComboUpdated(ComboInfo previous, ComboInfo current, IScheduler scheduler, Score score)
        {
            LastCombo = (current.ComboToReward, score.TotalScore);
        }
    }

    public sealed class NullCountdownViewmodel : ICountdownViewmodel
    {
        private NullCountdownViewmodel() { }
        public static readonly NullCountdownViewmodel Instance = new NullCountdownViewmodel();

        public int MaxMillis => 1;
        public float MaxMillisAsSingle => 1;
        public int RemainingMillis => 1;
        public TimeSpan Time => default;
        public (Combo, int score) LastCombo => (Combo.Empty, 0);
        public int Score => 0;
        public int EnemiesRemaining(BCZ.Core.Color color) => 0;
    }

    public sealed class CountdownSmoother
    {
        private ICountdownViewmodel vm;
        private float smoothed;
        const float smoothingFactor = 0.25f;

        public float Smoothed => smoothed;

        public CountdownSmoother(ICountdownViewmodel vm)
        {
            this.vm = vm;
            Reset(vm);
        }

        public void Reset(ICountdownViewmodel vm)
        {
            this.vm = vm;
            Update(99);
        }

        public void Update(float elapsedSeconds)
        {
            var vm = this.vm;
            var target = vm.RemainingMillis / vm.MaxMillisAsSingle;
            float maxJumpRestore = elapsedSeconds * smoothingFactor;
            smoothed = Math.Min(target, smoothed + maxJumpRestore);
        }
    }
}
